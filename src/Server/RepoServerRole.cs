using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>按角色和领域隔离 JSON 存档，支持内存延迟落地及原子替换回滚。</summary>
public sealed class RepoServerRole
{
    /// <summary>成功提交后的诊断回调，不参与服务端业务决策。</summary>
    private readonly Action<string, string>? _writeLog;
    /// <summary>服务端存档的绝对目录，由运行入口或测试注入。</summary>
    private readonly string _directory;
    /// <summary>延迟模式持有包含墓碑的完整角色快照，磁盘模式为空。</summary>
    private readonly Dictionary<long, DataServerRole>? _cache;
    /// <summary>按角色合并待写领域：1 为角色，2 为背包。</summary>
    private readonly Dictionary<long, int> _dirty = new();
    /// <summary>首次访问后不再从磁盘重新加载业务状态。</summary>
    private bool _loaded;
    /// <summary>便于调试查看的 JSON 写入设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>角色序列化元数据直接排除背包，避免移动时遍历整份物品。</summary>
    private static readonly JsonSerializerOptions RoleJsonOptions = CreateRoleJsonOptions();

    /// <summary>绑定存储目录，可选择内存优先并在 Flush 节点落地。</summary>
    public RepoServerRole(string directory, Action<string, string>? writeLog = null, bool deferWrites = false)
    {
        _directory = Path.GetFullPath(directory);
        _writeLog = writeLog;
        if (deferWrites) _cache = new();
    }

    /// <summary>角色属性、位置与墓碑的领域文件。</summary>
    public string GetRolePath(long roleId) => GetPath(roleId, "role");

    /// <summary>背包容量、实例与版本的领域文件。</summary>
    public string GetBagPath(long roleId) => GetPath(roleId, "bag");

    /// <summary>兼容读取的旧聚合存档，迁移后保留作为备份。</summary>
    public string GetLegacyPath(long roleId) => GetPath(roleId, null);

    /// <summary>读取完整角色，恢复中断提交后组合领域，缺失不补建。</summary>
    public DataServerRole Load(long roleId)
    {
        if (_cache is not null)
        {
            EnsureCache();
            if (!_cache.TryGetValue(roleId, out var cached) || cached.IsDeleted)
                throw new InvalidDataException("角色不存在或已删除");
            return cached;
        }
        Recover(roleId);
        var role = Read(roleId);
        if (role.IsDeleted) throw new InvalidDataException("角色已删除");
        return role;
    }

    /// <summary>通用聚合保存只写变化领域；创建和迁移提交完整领域组。</summary>
    public void Save(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 3); return; }
        Recover(role.RoleId);
        var rolePath = GetRolePath(role.RoleId);
        var bagPath = GetBagPath(role.RoleId);
        var roleContent = SerializeRole(role);
        var bagContent = JsonSerializer.Serialize(DataServerBagFile.FromRole(role), JsonOptions);
        var changed = new Dictionary<string, string>();
        if (File.Exists(rolePath) || File.Exists(bagPath))
        {
            _ = Read(role.RoleId);
            if (!JsonEquals(File.ReadAllText(bagPath), bagContent)) changed.Add("bag", bagContent);
            if (!JsonEquals(File.ReadAllText(rolePath), roleContent)) changed.Add("role", roleContent);
        }
        else
        {
            // role 是新格式的存在标记，最后替换，旧文件在提交前仍为读取来源。
            changed.Add("bag", bagContent);
            changed.Add("role", roleContent);
        }
        Commit(role.RoleId, changed);
    }

    /// <summary>正常移动和删除只写角色领域，旧格式首次写入时统一迁移。</summary>
    public void SaveRole(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 1); return; }
        Recover(role.RoleId);
        if (!File.Exists(GetRolePath(role.RoleId))) { Save(role); return; }
        if (!File.Exists(GetBagPath(role.RoleId))) throw new InvalidDataException($"缺少背包存档：{GetBagPath(role.RoleId)}");
        var content = SerializeRole(role);
        if (!JsonEquals(File.ReadAllText(GetRolePath(role.RoleId)), content))
            Commit(role.RoleId, new Dictionary<string, string> { ["role"] = content });
    }

    /// <summary>正常物品操作只写背包领域，不读取或替换角色属性文件。</summary>
    public void SaveBag(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 2); return; }
        Recover(role.RoleId);
        if (!File.Exists(GetRolePath(role.RoleId))) { Save(role); return; }
        if (!File.Exists(GetBagPath(role.RoleId))) throw new InvalidDataException($"缺少背包存档：{GetBagPath(role.RoleId)}");
        var content = JsonSerializer.Serialize(DataServerBagFile.FromRole(role), JsonOptions);
        if (!JsonEquals(File.ReadAllText(GetBagPath(role.RoleId)), content)) Commit(role.RoleId, new Dictionary<string, string>
        {
            ["bag"] = content
        });
    }

    /// <summary>恢复中断创建后枚举角色，领域文件和旧文件按编号去重，隐藏墓碑。</summary>
    public IReadOnlyList<DataServerRole> List()
    {
        if (_cache is not null)
        {
            EnsureCache();
            return _cache.Values.Where(role => !role.IsDeleted).OrderBy(role => role.RoleId).ToList().AsReadOnly();
        }
        Directory.CreateDirectory(_directory);
        foreach (var path in Directory.EnumerateFiles(_directory, "*.pending.json").ToArray())
        {
            var prefix = Path.GetFileName(path)[..^".pending.json".Length];
            if (long.TryParse(prefix, out var id) && id > 0) Recover(id);
        }
        return StoredIds().Select(Read).Where(role => !role.IsDeleted).ToList().AsReadOnly();
    }

    /// <summary>完整加载成功后才接管快照，避免损坏文件留下部分缓存。</summary>
    private void EnsureCache()
    {
        if (_loaded || _cache is null) return;
        var disk = new RepoServerRole(_directory, _writeLog);
        _ = disk.List();
        var roles = disk.StoredIds().Select(disk.Read).ToArray();
        foreach (var role in roles) _cache.Add(role.RoleId, role);
        _loaded = true;
    }

    /// <summary>合并领域到最新缓存，不在数据修改时执行存档 IO。</summary>
    private void Stage(DataServerRole role, int domains)
    {
        EnsureCache();
        if (_cache!.TryGetValue(role.RoleId, out var previous))
        {
            if (domains == 1) role = role with { Bag = previous.Bag, BagExtraRows = previous.BagExtraRows };
            if (domains == 2) role = previous with { Bag = role.Bag, BagExtraRows = role.BagExtraRows };
            if (role == previous) return;
        }
        else domains = 3;
        _cache[role.RoleId] = role;
        _dirty[role.RoleId] = _dirty.GetValueOrDefault(role.RoleId) | domains;
    }

    /// <summary>只检查待写角色，成功清标记；失败保留并继续其他角色，最后汇总错误。</summary>
    public void Flush(IReadOnlySet<long>? excluded = null)
    {
        if (_cache is null || _dirty.Count == 0) return;
        var disk = new RepoServerRole(_directory, _writeLog);
        var errors = new List<Exception>();
        foreach (var (id, domains) in _dirty.ToArray())
        {
            if (excluded?.Contains(id) == true) continue;
            try
            {
                if (domains == 1) disk.SaveRole(_cache[id]);
                else if (domains == 2) disk.SaveBag(_cache[id]);
                else disk.Save(_cache[id]);
                _dirty.Remove(id);
            }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("服务端缓存落地失败，保留待写数据供重试", errors);
    }

    /// <summary>共同提交时写指定角色但不清待写标记，日志暂存在外层。</summary>
    internal void WritePending(IEnumerable<long> ids, Action<string, string> log)
    {
        var disk = new RepoServerRole(_directory, log);
        foreach (var id in ids.OrderBy(id => id))
        {
            if (!_dirty.TryGetValue(id, out var domains)) continue;
            if (domains == 1) disk.SaveRole(_cache![id]);
            else if (domains == 2) disk.SaveBag(_cache![id]);
            else disk.Save(_cache![id]);
        }
    }

    /// <summary>外层提交成功后才确认参与角色的待写领域。</summary>
    internal void AcceptPending(IEnumerable<long> ids)
    {
        foreach (var id in ids) _dirty.Remove(id);
    }

    /// <summary>统一生成固定领域路径，拒绝无效角色编号。</summary>
    private string GetPath(long roleId, string? domain)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        return Path.Combine(_directory, domain is null ? $"{roleId}.json" : $"{roleId}.{domain}.json");
    }

    /// <summary>新领域存在时严格组合，只有两份领域文件都不存在才读取旧聚合。</summary>
    private DataServerRole Read(long roleId)
    {
        var path = GetRolePath(roleId);
        try
        {
            DataServerRole role;
            if (File.Exists(path) || Directory.Exists(path) ||
                File.Exists(GetBagPath(roleId)) || Directory.Exists(GetBagPath(roleId)))
            {
                role = JsonSerializer.Deserialize<DataServerRole>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("角色数据为空");
                ValidateRole(role);
                if (role.RoleId != roleId) throw new InvalidDataException("角色 ID 与文件名不符");
                path = GetBagPath(roleId);
                var bag = JsonSerializer.Deserialize<DataServerBagFile>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("背包数据为空");
                if (bag.RoleId != roleId) throw new InvalidDataException("背包角色 ID 与文件名不符");
                role = role with { BagExtraRows = bag.BagExtraRows, Bag = bag.Bag };
            }
            else
            {
                path = GetLegacyPath(roleId);
                if (!File.Exists(path) && !Directory.Exists(path)) throw new InvalidDataException("角色不存在");
                role = JsonSerializer.Deserialize<DataServerRole>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("角色数据为空");
            }
            role = role with { Bag = RestoreEquipmentRevision(role.Bag, File.ReadAllText(path)) };
            Validate(role);
            if (role.RoleId != roleId) throw new InvalidDataException("角色 ID 与文件名不符");
            return role;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new InvalidDataException($"无法读取角色存档：{path}；{error.Message}", error);
        }
    }

    /// <summary>只在旧存档缺少装备版本时继承原物品版本，读取不触发落地。</summary>
    private static DataServerBag RestoreEquipmentRevision(DataServerBag bag, string json)
    {
        if (bag is null) throw new InvalidDataException("角色背包字段无效");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("Bag", out var payload) &&
            payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("EquipmentRevision", out _)) return bag;
        return bag with { EquipmentRevision = bag.Revision };
    }

    /// <summary>角色领域排除背包字段，保留聚合类型的业务兼容性。</summary>
    private static string SerializeRole(DataServerRole role) => JsonSerializer.Serialize(role, RoleJsonOptions);

    /// <summary>只对角色聚合裁剪领域外属性，旧存档反序列化仍使用完整契约。</summary>
    private static JsonSerializerOptions CreateRoleJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(DataServerRole)) return;
            foreach (var property in info.Properties.Where(property => property.Name is
                         nameof(DataServerRole.Bag) or nameof(DataServerRole.BagExtraRows)).ToArray())
                info.Properties.Remove(property);
        });
        return new JsonSerializerOptions(JsonOptions) { TypeInfoResolver = resolver };
    }

    /// <summary>比较 JSON 值，忽略磁盘文本的缩进和字段排版差异。</summary>
    private static bool JsonEquals(string left, string right) => System.Text.Json.Nodes.JsonNode.DeepEquals(
        System.Text.Json.Nodes.JsonNode.Parse(left), System.Text.Json.Nodes.JsonNode.Parse(right));

    /// <summary>单领域原子替换，多领域先落回滚记录，全部成功后才记录目标 IO。</summary>
    private void Commit(long roleId, Dictionary<string, string> changed)
    {
        if (changed.Count == 0) return;
        Directory.CreateDirectory(_directory);
        var pending = GetPath(roleId, "pending");
        if (changed.Count > 1)
        {
            var originals = changed.Keys.ToDictionary(domain => domain,
                domain => File.Exists(GetPath(roleId, domain)) ? File.ReadAllText(GetPath(roleId, domain)) : null);
            WriteAtomic(pending, JsonSerializer.Serialize(originals, JsonOptions));
        }
        try
        {
            foreach (var (domain, content) in changed) WriteAtomic(GetPath(roleId, domain), content);
            if (changed.Count > 1) File.Delete(pending);
        }
        catch
        {
            Recover(roleId);
            throw;
        }
        foreach (var (domain, content) in changed)
        {
            try { _writeLog?.Invoke(GetPath(roleId, domain), content); }
            catch (Exception error) { System.Diagnostics.Trace.WriteLine(error); }
        }
    }

    /// <summary>回滚未提交的领域组，失败保留记录供后续恢复，禁止接受任意路径。</summary>
    private void Recover(long roleId)
    {
        var pending = GetPath(roleId, "pending");
        if (!File.Exists(pending)) return;
        try
        {
            var originals = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(pending))
                ?? throw new InvalidDataException("回滚记录为空");
            if (originals.Count != 2 || !originals.ContainsKey("role") || !originals.ContainsKey("bag"))
                throw new InvalidDataException("回滚记录包含非法领域");
            foreach (var (domain, content) in originals)
            {
                var path = GetPath(roleId, domain);
                if (content is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else if (!File.Exists(path) || File.ReadAllText(path) != content) WriteAtomic(path, content);
            }
            File.Delete(pending);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new IOException($"无法恢复未提交存档：{pending}；{error.Message}", error);
        }
    }

    /// <summary>同目录写入临时文件并刷盘，成功替换后清理临时残留。</summary>
    private static void WriteAtomic(string path, string content)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(System.Text.Encoding.UTF8.GetBytes(content));
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"无法保存角色存档：{path}；{error.Message}", error);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>校验名称及外观，提交仓库状态后允许列表展示新角色。</summary>
    public DataServerRole Create(string name, ServerRoleGender gender, int headId, int mapId = 1, float? spawnX = null, float? spawnY = null)
    {
        var roles = List();
        name = name.Trim();
        if (name.Length is < 1 or > 16 || name.Any(char.IsControl))
            throw new ArgumentException("角色名字需为 1 至 16 个字符，且不能包含控制字符");
        if (roles.Any(role => string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("角色名字已存在");
        if (!Enum.IsDefined(gender) || headId is < 1 or > 74) throw new ArgumentException("性别或头型无效");
        var id = checked((_cache is null ? StoredIds() : _cache.Keys).DefaultIfEmpty(1002).Max() + 1);
        var role = new DataServerRole
        {
            RoleId = id, MapId = mapId, Name = name, Gender = gender, HeadId = headId, Level = 1,
            Health = 100, MaxHealth = 100, Mana = 60, MaxMana = 60, Experience = 0, NextExperience = 500,
            X = spawnX ?? (gender == ServerRoleGender.Male ? 16016 : 16144),
            Y = spawnY ?? (gender == ServerRoleGender.Male ? 16016 : 16080)
        };
        Save(role);
        return role;
    }

    /// <summary>提交墓碑，延迟模式在落地节点写文件；删除编号不再复用。</summary>
    public void Delete(long roleId)
    {
        var role = Load(roleId);
        SaveRole(role with { IsDeleted = true });
    }

    /// <summary>只识别旧角色和 role 领域，忽略背包、事务与临时文件。</summary>
    private IEnumerable<long> StoredIds() => Directory.EnumerateFiles(_directory, "*.json")
        .Select(path => Path.GetFileName(path))
        .Select(name => name.EndsWith(".role.json", StringComparison.Ordinal)
            ? name[..^".role.json".Length] : name[..^".json".Length])
        .Select(name => long.TryParse(name, out var id) ? id : 0)
        .Where(id => id > 0).Distinct().OrderBy(id => id);

    /// <summary>拒绝无法成为合法角色快照的存档数据。</summary>
    public static void Validate(DataServerRole role)
    {
        ValidateRole(role);
        if (role.BagExtraRows < 0 || role.Bag is null) throw new InvalidDataException("角色背包字段无效");
        role.Bag.Validate();
    }

    /// <summary>先诊断角色领域自身字段，避免将角色损坏归因到背包路径。</summary>
    private static void ValidateRole(DataServerRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.MapId <= 0 || role.RoleId <= 0 || string.IsNullOrWhiteSpace(role.Name) || role.Level <= 0 ||
            role.MaxHealth <= 0 || role.Health < 0 || role.Health > role.MaxHealth ||
            role.MaxMana < 0 || role.Mana < 0 || role.Mana > role.MaxMana ||
            role.Experience < 0 || role.NextExperience <= 0 ||
            !float.IsFinite(role.X) || !float.IsFinite(role.Y) || !Enum.IsDefined(role.Gender) ||
            role.HeadId < 1 || role.HeadId > 74 || role.Revision < 0 || role.AcknowledgedAction < 0)
            throw new InvalidDataException("角色存档包含无效字段");
    }
}
