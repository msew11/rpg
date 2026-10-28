using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Rpg.Server.Data;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server;

/// <summary>按角色实体表延迟落地，角色列表优先读取独立摘要快照。</summary>
public sealed class RepoServerRole
{
    /// <summary>成功组提交后的诊断日志。</summary>
    private readonly Action<string, string>? _writeLog;
    /// <summary>实体文件根目录。</summary>
    private readonly string _directory;
    /// <summary>仅已选择或本次修改角色才进入完整实体缓存。</summary>
    private readonly Dictionary<long, RepoEntityCache>? _cache;
    /// <summary>登录加载的独立摘要及删除墓碑。</summary>
    private readonly Dictionary<long, RoleSummaryEntity> _summaries = new();
    /// <summary>仅角色单例的未落地删除，不要求加载物品表。</summary>
    private readonly Dictionary<long, RoleEntity> _deletions = new();
    /// <summary>待写领域：1 角色、2 物品、3 完整、4 未加载角色删除。</summary>
    private readonly Dictionary<long, int> _dirty = new();
    /// <summary>摘要是否已经完整加载。</summary>
    private bool _loaded;
    /// <summary>实体表使用与配置一致的 camelCase 对象或无包装数组。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
    };
    /// <summary>兼容旧 PascalCase 存档，不改变历史文件。</summary>
    private static readonly JsonSerializerOptions LegacyOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>绑定实体目录，可选择缓存优先与延迟落地。</summary>
    public RepoServerRole(string directory, Action<string, string>? writeLog = null, bool deferWrites = false)
    {
        _directory = Path.GetFullPath(directory);
        _writeLog = writeLog;
        if (deferWrites) _cache = new();
    }

    /// <summary>角色单例表路径。</summary>
    public string GetRolePath(long roleId) => GetPath(roleId, "role");
    /// <summary>物品多例表路径。</summary>
    public string GetItemPath(long roleId) => GetPath(roleId, "item");
    /// <summary>装备扩展多例表路径。</summary>
    public string GetEquipmentPath(long roleId) => GetPath(roleId, "equipment");
    /// <summary>登录展示快照路径。</summary>
    public string GetSummaryPath(long roleId) => GetPath(roleId, "role_summary");
    /// <summary>只供兼容读取的旧背包领域路径。</summary>
    public string GetBagPath(long roleId) => GetPath(roleId, "bag");
    /// <summary>只供兼容读取的旧聚合存档路径。</summary>
    public string GetLegacyPath(long roleId) => GetPath(roleId, null);

    /// <summary>只加载指定角色的全部实体表；其他角色仍只有摘要。</summary>
    public DataServerRole Load(long roleId)
    {
        if (_cache is not null)
        {
            EnsureCache();
            if (!_summaries.TryGetValue(roleId, out var summary) || summary.IsDeleted)
                throw new InvalidDataException("角色不存在或已删除");
            if (_cache.TryGetValue(roleId, out var cached)) return cached.ToRole();
        }
        Recover(roleId);
        var role = Read(roleId);
        if (role.IsDeleted) throw new InvalidDataException("角色已删除");
        if (_cache is not null) _cache.Add(roleId, new RepoEntityCache(role));
        return role;
    }

    /// <summary>兼容完整列表接口；登录及角色管理使用 ListSummaries 避免完整加载。</summary>
    public IReadOnlyList<DataServerRole> List() => ListSummaries().Select(value => Load(value.RoleId)).ToList().AsReadOnly();

    /// <summary>首次只读取全部摘要，旧格式无摘要时兼容生成内存快照。</summary>
    public IReadOnlyList<RoleSummaryEntity> ListSummaries()
    {
        if (_cache is not null) EnsureCache();
        else LoadSummaries();
        return _summaries.Values.Where(value => !value.IsDeleted).OrderBy(value => value.RoleId).ToList().AsReadOnly();
    }

    /// <summary>摘要完整校验后一次发布，不留下部分登录列表。</summary>
    private void LoadSummaries()
    {
        if (!Directory.Exists(_directory)) { _summaries.Clear(); return; }
        foreach (var path in Directory.EnumerateFiles(_directory, "*.pending.json").ToArray())
        {
            var prefix = Path.GetFileName(path)[..^".pending.json".Length];
            if (long.TryParse(prefix, out var id) && id > 0) Recover(id);
        }
        var values = new Dictionary<long, RoleSummaryEntity>();
        foreach (var id in StoredIds())
        {
            RoleSummaryEntity value;
            var path = GetSummaryPath(id);
            if (File.Exists(path) || Directory.Exists(path)) value = ReadJson<RoleSummaryEntity>(path, JsonOptions);
            else value = RoleSummaryEntity.FromRole(Read(id));
            ValidateSummary(value, id);
            values.Add(id, value);
        }
        _summaries.Clear();
        foreach (var (id, value) in values) _summaries.Add(id, value);
    }

    /// <summary>首次访问只初始化摘要索引，不预读任何新格式物品表。</summary>
    private void EnsureCache()
    {
        if (_loaded) return;
        LoadSummaries();
        _loaded = true;
    }

    /// <summary>完整保存只提交发生变化的实体文件，首次迁移提交全部表。</summary>
    public void Save(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 3); return; }
        Recover(role.RoleId);
        var cache = new RepoEntityCache(role);
        Commit(role.RoleId, Changed(role.RoleId, Serialize(cache)));
    }

    /// <summary>移动只写角色单例，展示字段变化时同时更新摘要。</summary>
    public void SaveRole(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 1); return; }
        Recover(role.RoleId);
        if (!File.Exists(GetSummaryPath(role.RoleId))) { Save(role); return; }
        var previous = ReadRoleEntity(role.RoleId);
        var cache = new RepoEntityCache(role);
        var entity = cache.Role.Value with { NextInstanceId = previous.NextInstanceId, BagExtraRows = previous.BagExtraRows };
        var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(role.RoleId), JsonOptions) with
        {
            Name = role.Name, Level = role.Level, Gender = role.Gender, HeadId = role.HeadId, IsDeleted = role.IsDeleted
        };
        Commit(role.RoleId, Changed(role.RoleId, new Dictionary<string, string>
        {
            ["role"] = JsonSerializer.Serialize(entity, JsonOptions),
            ["role_summary"] = JsonSerializer.Serialize(summary, JsonOptions)
        }));
    }

    /// <summary>物品变更拆为实例与扩展表；编号、扩容和展示有变化时共同提交。</summary>
    public void SaveBag(DataServerRole role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 2); return; }
        Recover(role.RoleId);
        if (!File.Exists(GetSummaryPath(role.RoleId))) { Save(role); return; }
        var cache = new RepoEntityCache(role);
        var values = Serialize(cache);
        var previous = ReadRoleEntity(role.RoleId);
        values["role"] = JsonSerializer.Serialize(previous with
        {
            BagExtraRows = role.BagExtraRows, NextInstanceId = role.Bag.NextInstanceId
        }, JsonOptions);
        var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(role.RoleId), JsonOptions) with
        {
            EquippedItems = RoleSummaryEntity.FromRole(role).EquippedItems
        };
        values["role_summary"] = JsonSerializer.Serialize(summary, JsonOptions);
        Commit(role.RoleId, Changed(role.RoleId, values));
    }

    /// <summary>把业务候选拆成实体缓存，并同步内存摘要供返回选角读取。</summary>
    private void Stage(DataServerRole role, int domains)
    {
        EnsureCache();
        if (_summaries.ContainsKey(role.RoleId))
        {
            var previous = _cache!.TryGetValue(role.RoleId, out var cached) ? cached.ToRole() : Read(role.RoleId);
            if (domains == 1) role = role with { Bag = previous.Bag };
            if (domains == 2) role = previous with { Bag = role.Bag, BagExtraRows = role.BagExtraRows };
            if (role == previous) return;
        }
        else domains = 3;
        _cache![role.RoleId] = new RepoEntityCache(role);
        _summaries[role.RoleId] = RoleSummaryEntity.FromRole(role);
        _dirty[role.RoleId] = _dirty.GetValueOrDefault(role.RoleId) | domains;
    }

    /// <summary>只检查待写角色，失败保持已接纳内存并继续其他角色。</summary>
    public void Flush(IReadOnlySet<long>? excluded = null)
    {
        if (_cache is null || _dirty.Count == 0) return;
        var errors = new List<Exception>();
        foreach (var id in _dirty.Keys.ToArray())
        {
            if (excluded?.Contains(id) == true) continue;
            try
            {
                WriteOne(id, _writeLog);
                _dirty.Remove(id);
                _deletions.Remove(id);
            }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0) throw new AggregateException("服务端缓存落地失败，保留待写数据供重试", errors);
    }

    /// <summary>共同地图转移先写文件，由外层提交点决定何时清标记。</summary>
    internal void WritePending(IEnumerable<long> ids, Action<string, string> log)
    {
        foreach (var id in ids.OrderBy(id => id))
            if (_dirty.ContainsKey(id)) WriteOne(id, log);
    }

    /// <summary>按待写领域调用磁盘仓库，不提前清除重试状态。</summary>
    private void WriteOne(long id, Action<string, string>? log)
    {
        var disk = new RepoServerRole(_directory, log);
        var domains = _dirty[id];
        if (domains == 4) { disk.Delete(id); return; }
        var role = _cache![id].ToRole();
        if (domains == 1) disk.SaveRole(role);
        else if (domains == 2) disk.SaveBag(role);
        else disk.Save(role);
    }

    /// <summary>外层共同提交成功后确认全部参与角色。</summary>
    internal void AcceptPending(IEnumerable<long> ids)
    {
        foreach (var id in ids) { _dirty.Remove(id); _deletions.Remove(id); }
    }

    /// <summary>生成固定领域路径，拒绝无效身份。</summary>
    private string GetPath(long roleId, string? domain)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        return Path.Combine(_directory, domain is null ? $"{roleId}.json" : $"{roleId}.{domain}.json");
    }

    /// <summary>新格式读取实体表并交由主键缓存校验；旧格式仅作为未迁移来源。</summary>
    private DataServerRole Read(long roleId)
    {
        if (File.Exists(GetSummaryPath(roleId)) || File.Exists(GetItemPath(roleId)) || File.Exists(GetEquipmentPath(roleId)))
        {
            var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(roleId), JsonOptions);
            ValidateSummary(summary, roleId);
            var role = ReadRoleEntity(roleId);
            try
            {
                var items = new ItemEntityCache(ReadJson<ItemEntity[]>(GetItemPath(roleId), JsonOptions));
                var equipment = new EquipmentEntityCache(ReadJson<EquipmentEntity[]>(GetEquipmentPath(roleId), JsonOptions), items);
                return new RepoEntityCache(new RoleEntityCache(role), items, equipment).ToRole();
            }
            catch (InvalidDataException error)
            { throw new InvalidDataException($"角色实体表无效：{GetItemPath(roleId)}；{GetEquipmentPath(roleId)}；{error.Message}", error); }
        }
        var path = GetRolePath(roleId);
        DataServerRole legacy;
        if (File.Exists(path) || Directory.Exists(path) || File.Exists(GetBagPath(roleId)) || Directory.Exists(GetBagPath(roleId)))
        {
            legacy = ReadJson<DataServerRole>(path, LegacyOptions);
            var bag = ReadJson<DataServerBagFile>(GetBagPath(roleId), LegacyOptions);
            if (bag.Bag is null) throw new InvalidDataException($"角色背包字段无效：{GetBagPath(roleId)}");
            if (bag.RoleId != roleId) throw new InvalidDataException($"背包角色 ID 与文件名不符：{GetBagPath(roleId)}");
            legacy = legacy with { BagExtraRows = bag.BagExtraRows, Bag = bag.Bag };
        }
        else legacy = ReadJson<DataServerRole>(GetLegacyPath(roleId), LegacyOptions);
        Validate(legacy);
        if (legacy.RoleId != roleId) throw new InvalidDataException($"角色 ID 与文件名不符：{path}");
        return legacy;
    }

    /// <summary>读取并校验独立角色单例的身份。</summary>
    private RoleEntity ReadRoleEntity(long roleId)
    {
        var value = ReadJson<RoleEntity>(GetRolePath(roleId), JsonOptions);
        if (value.RoleId != roleId) throw new InvalidDataException($"角色 ID 与文件名不符：{GetRolePath(roleId)}");
        return value;
    }

    /// <summary>统一 JSON 文件诊断，保留出错路径且不覆盖原始文件。</summary>
    private static T ReadJson<T>(string path, JsonSerializerOptions options)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options) ?? throw new InvalidDataException("数据为空"); }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        { throw new InvalidDataException($"无法读取角色存档：{path}；{error.Message}", error); }
    }

    /// <summary>单例对象与多例数组直接序列化，不含版本及外层包装。</summary>
    private static Dictionary<string, string> Serialize(RepoEntityCache cache) => new()
    {
        ["item"] = JsonSerializer.Serialize(cache.Item.Items.Values.OrderBy(item => item.InstanceId), JsonOptions),
        ["equipment"] = JsonSerializer.Serialize(cache.Equipment.Items.Values.OrderBy(item => item.InstanceId), JsonOptions),
        ["role"] = JsonSerializer.Serialize(cache.Role.Value, JsonOptions),
        ["role_summary"] = JsonSerializer.Serialize(RoleSummaryEntity.FromRole(cache.ToRole()), JsonOptions)
    };

    /// <summary>比较实际 JSON 值，首次创建和迁移要求完整领域组。</summary>
    private Dictionary<string, string> Changed(long id, Dictionary<string, string> values)
    {
        var changed = new Dictionary<string, string>();
        foreach (var (domain, content) in values)
        {
            var path = GetPath(id, domain);
            if (!File.Exists(path) || !JsonEquals(File.ReadAllText(path), content)) changed.Add(domain, content);
        }
        return changed;
    }

    /// <summary>摘要自身完整校验，不依赖物品表或角色完整数据。</summary>
    private static void ValidateSummary(RoleSummaryEntity value, long id)
    {
        if (value.RoleId != id || id <= 0 || string.IsNullOrWhiteSpace(value.Name) || value.Level <= 0 ||
            !Enum.IsDefined(value.Gender) || value.HeadId is < 1 or > 74 || value.EquippedItems.Any(item => item <= 0))
            throw new InvalidDataException($"角色摘要包含无效字段：{id}.role_summary.json");
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
            if (originals.Count < 2 || originals.Keys.Any(domain => domain is not ("role" or "bag" or "item" or "equipment" or "role_summary")))
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
        var roles = ListSummaries();
        name = name.Trim();
        if (name.Length is < 1 or > 16 || name.Any(char.IsControl))
            throw new ArgumentException("角色名字需为 1 至 16 个字符，且不能包含控制字符");
        if (roles.Any(role => string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("角色名字已存在");
        if (!Enum.IsDefined(gender) || headId is < 1 or > 74) throw new ArgumentException("性别或头型无效");
        var id = checked((_cache is null ? StoredIds() : _summaries.Keys).DefaultIfEmpty(1002).Max() + 1);
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

    /// <summary>仅加载角色单例即可删除，不触碰未选择角色的物品表。</summary>
    public void Delete(long roleId)
    {
        var summary = ListSummaries().SingleOrDefault(value => value.RoleId == roleId)
            ?? throw new InvalidDataException("角色不存在或已删除");
        if (_cache is not null && _cache.TryGetValue(roleId, out var cached))
        {
            SaveRole(cached.ToRole() with { IsDeleted = true });
            return;
        }
        if (!File.Exists(GetSummaryPath(roleId)))
        {
            SaveRole(Load(roleId) with { IsDeleted = true });
            return;
        }
        var value = ReadRoleEntity(roleId) with { IsDeleted = true };
        var deleted = summary with { IsDeleted = true };
        if (_cache is not null)
        {
            _deletions[roleId] = value;
            _summaries[roleId] = deleted;
            _dirty[roleId] = 4;
            return;
        }
        Commit(roleId, Changed(roleId, new Dictionary<string, string>
        {
            ["role"] = JsonSerializer.Serialize(value, JsonOptions),
            ["role_summary"] = JsonSerializer.Serialize(deleted, JsonOptions)
        }));
    }

    /// <summary>从完整角色、摘要或旧文件识别编号，忽略实例表和事务文件。</summary>
    private IEnumerable<long> StoredIds() => (Directory.Exists(_directory) ? Directory.EnumerateFiles(_directory, "*.json") : Array.Empty<string>())
        .Select(Path.GetFileName)
        .Select(name => name!.EndsWith(".role_summary.json", StringComparison.Ordinal)
            ? name[..^".role_summary.json".Length] : name.EndsWith(".role.json", StringComparison.Ordinal)
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
