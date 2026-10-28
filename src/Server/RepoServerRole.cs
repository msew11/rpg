using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
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
    private readonly Dictionary<long, RoleEntityTables>? _cache;
    /// <summary>登录加载的独立摘要及删除墓碑。</summary>
    private readonly Dictionary<long, RoleSummaryEntity> _summaries;
    /// <summary>仅角色单例的未落地删除，不要求加载物品表。</summary>
    private readonly Dictionary<long, RoleEntity> _deletions;
    /// <summary>待写领域：1 角色、2 物品、3 完整、4 未加载角色删除。</summary>
    private readonly Dictionary<long, int> _dirty;
    /// <summary>摘要是否已经完整加载。</summary>
    private bool _loaded { get => Cache.SummariesLoaded; set => Cache.SummariesLoaded = value; }
    /// <summary>仓库使用的纯内存实体入口，可独立传给 Handler。</summary>
    public RepoEntityCache Cache { get; }
    /// <summary>实体表使用与配置一致的 camelCase 对象或无包装数组。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>绑定实体目录，可选择缓存优先与延迟落地。</summary>
    public RepoServerRole(string directory, Action<string, string>? writeLog = null, bool deferWrites = false, RepoEntityCache? cache = null)
    {
        _directory = Path.GetFullPath(directory);
        _writeLog = writeLog;
        Cache = cache ?? new RepoEntityCache();
        _summaries = Cache.Summaries;
        _deletions = Cache.Deletions;
        _dirty = Cache.Dirty;
        if (deferWrites) _cache = Cache.Roles;
    }

    /// <summary>角色单例表路径。</summary>
    public string GetRolePath(long roleId) => GetPath(roleId, "role");
    /// <summary>物品多例表路径。</summary>
    public string GetItemPath(long roleId) => GetPath(roleId, "item");
    /// <summary>装备扩展多例表路径。</summary>
    public string GetEquipmentPath(long roleId) => GetPath(roleId, "equipment");
    /// <summary>登录展示快照路径。</summary>
    public string GetSummaryPath(long roleId) => GetPath(roleId, "role_summary");

    /// <summary>只加载指定角色的全部实体表；其他角色仍只有摘要。</summary>
    public RoleEntityTables Load(long roleId)
    {
        if (_cache is not null)
        {
            EnsureCache();
            if (!_summaries.TryGetValue(roleId, out var summary) || summary.IsDeleted)
                throw new InvalidDataException("角色不存在或已删除");
            if (_cache.TryGetValue(roleId, out var cached)) return cached;
        }
        Recover(roleId);
        var role = Read(roleId);
        if (role.Role.Value.IsDeleted) throw new InvalidDataException("角色已删除");
        if (_cache is not null) _cache.Add(roleId, role);
        return role;
    }

    /// <summary>按需加载完整角色列表；登录及角色管理只读取 ListSummaries。</summary>
    public IReadOnlyList<RoleEntityTables> List() => ListSummaries().Select(value => Load(value.RoleId)).ToList().AsReadOnly();

    /// <summary>首次只读取全部摘要，不通过完整角色数据回填。</summary>
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
            var path = GetSummaryPath(id);
            var value = ReadJson<RoleSummaryEntity>(path, JsonOptions);
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

    /// <summary>完整保存只提交发生变化的实体文件，创建时提交全部表。</summary>
    public void Save(RoleEntityTables role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 3); return; }
        Recover(role.Role.Value.RoleId);
        var cache = role;
        Commit(role.Role.Value.RoleId, Changed(role.Role.Value.RoleId, Serialize(cache)));
    }

    /// <summary>移动只写角色单例，展示字段变化时同时更新摘要。</summary>
    public void SaveRole(RoleEntityTables role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 1); return; }
        Recover(role.Role.Value.RoleId);
        var previous = ReadRoleEntity(role.Role.Value.RoleId);
        var entity = role.Role.Value with { NextInstanceId = previous.NextInstanceId, BagExtraRows = previous.BagExtraRows };
        var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(role.Role.Value.RoleId), JsonOptions) with
        {
            Name = entity.Name, Level = entity.Level, Gender = entity.Gender, HeadId = entity.HeadId, IsDeleted = entity.IsDeleted
        };
        Commit(entity.RoleId, Changed(entity.RoleId, new Dictionary<string, string>
        {
            ["role"] = JsonSerializer.Serialize(entity, JsonOptions),
            ["role_summary"] = JsonSerializer.Serialize(summary, JsonOptions)
        }));
    }

    /// <summary>物品变更拆为实例与扩展表；编号、扩容和展示有变化时共同提交。</summary>
    public void SaveBag(RoleEntityTables role)
    {
        Validate(role);
        if (_cache is not null) { Stage(role, 2); return; }
        Recover(role.Role.Value.RoleId);
        var cache = role;
        var values = Serialize(cache);
        var previous = ReadRoleEntity(role.Role.Value.RoleId);
        values["role"] = JsonSerializer.Serialize(previous with
        {
            BagExtraRows = role.Role.Value.BagExtraRows, NextInstanceId = role.Role.Value.NextInstanceId
        }, JsonOptions);
        var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(role.Role.Value.RoleId), JsonOptions) with
        {
            EquippedItems = RoleSummaryEntity.FromRole(role).EquippedItems
        };
        values["role_summary"] = JsonSerializer.Serialize(summary, JsonOptions);
        Commit(role.Role.Value.RoleId, Changed(role.Role.Value.RoleId, values));
    }

    /// <summary>在仓库边界显式准备原表，再把候选提交给纯内存入口。</summary>
    private void Stage(RoleEntityTables role, int domains)
    {
        EnsureCache();
        if (_summaries.ContainsKey(role.Role.Value.RoleId) && !_cache!.ContainsKey(role.Role.Value.RoleId))
            Load(role.Role.Value.RoleId);
        Cache.Commit(role, domains);
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
        if (domains == 4) { WriteDeletion(id); return; }
        var role = _cache![id];
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
    private string GetPath(long roleId, string domain)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        return Path.Combine(_directory, $"{roleId}.{domain}.json");
    }

    /// <summary>读取当前四张实体表，交由主键缓存和组合候选校验。</summary>
    private RoleEntityTables Read(long roleId)
    {
        var summary = ReadJson<RoleSummaryEntity>(GetSummaryPath(roleId), JsonOptions);
        ValidateSummary(summary, roleId);
        var role = ReadRoleEntity(roleId);
        try
        {
            var items = new ItemEntityCache(ReadJson<ItemEntity[]>(GetItemPath(roleId), JsonOptions));
            var equipment = new EquipmentEntityCache(ReadJson<EquipmentEntity[]>(GetEquipmentPath(roleId), JsonOptions), items);
            return new RoleEntityTables(new RoleEntityCache(role), items, equipment);
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"角色实体表无效：{GetItemPath(roleId)}；{GetEquipmentPath(roleId)}；{error.Message}", error); }
    }

    /// <summary>读取并校验独立角色单例的身份。</summary>
    private RoleEntity ReadRoleEntity(long roleId)
    {
        var value = ReadJson<RoleEntity>(GetRolePath(roleId), JsonOptions);
        if (value.RoleId != roleId) throw new InvalidDataException($"角色 ID 与文件名不符：{GetRolePath(roleId)}");
        try { RoleEntityCache.Validate(value); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"角色单例无效：{GetRolePath(roleId)}；{error.Message}", error); }
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
    private static Dictionary<string, string> Serialize(RoleEntityTables cache) => new()
    {
        ["item"] = JsonSerializer.Serialize(cache.Item.Items.Values.OrderBy(item => item.InstanceId), JsonOptions),
        ["equipment"] = JsonSerializer.Serialize(cache.Equipment.Items.Values.OrderBy(item => item.InstanceId), JsonOptions),
        ["role"] = JsonSerializer.Serialize(cache.Role.Value, JsonOptions),
        ["role_summary"] = JsonSerializer.Serialize(RoleSummaryEntity.FromRole(cache), JsonOptions)
    };

    /// <summary>比较实际 JSON 值，首次创建写入完整领域组。</summary>
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
            if (originals.Count < 2 || originals.Keys.Any(domain => domain is not ("role" or "item" or "equipment" or "role_summary")))
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

    /// <summary>显式准备摘要后复用纯内存创建业务，再在同步模式落地。</summary>
    public RoleEntityTables Create(string name, ServerRoleGender gender, int headId, int mapId = 1, float? spawnX = null, float? spawnY = null)
    {
        ListSummaries();
        var role = Handler.Role.RoleCreation.Create(Cache, name, gender, headId, mapId, spawnX, spawnY);
        Save(role);
        return role;
    }

    /// <summary>只加载删除所需的单例，不读取未选择物品表。</summary>
    public void PrepareDelete(long roleId)
    {
        ListSummaries();
        if (!_summaries.TryGetValue(roleId, out var summary) || summary.IsDeleted)
            throw new InvalidDataException("角色不存在或已删除");
        if (!Cache.Roles.ContainsKey(roleId) && !Cache.Deletions.ContainsKey(roleId))
            Cache.Deletions[roleId] = ReadRoleEntity(roleId);
    }

    /// <summary>准备后提交内存墓碑，文件写入仍由独立节点执行。</summary>
    public void Delete(long roleId)
    {
        PrepareDelete(roleId);
        Cache.Delete(roleId);
        if (_cache is null) WriteDeletion(roleId);
    }

    /// <summary>仅落地角色单例与摘要墓碑。</summary>
    private void WriteDeletion(long roleId)
    {
        var value = Cache.Roles.TryGetValue(roleId, out var tables) ? tables.Role.Value : _deletions[roleId];
        Commit(roleId, Changed(roleId, new Dictionary<string, string>
        {
            ["role"] = JsonSerializer.Serialize(value, JsonOptions),
            ["role_summary"] = JsonSerializer.Serialize(_summaries[roleId], JsonOptions)
        }));
    }

    /// <summary>从当前角色表和摘要识别编号，忽略其他文件。</summary>
    private IEnumerable<long> StoredIds() => (Directory.Exists(_directory) ? Directory.EnumerateFiles(_directory, "*.json") : Array.Empty<string>())
        .Select(Path.GetFileName)
        .Select(name => name!.EndsWith(".role_summary.json", StringComparison.Ordinal)
            ? name[..^".role_summary.json".Length] : name.EndsWith(".role.json", StringComparison.Ordinal)
                ? name[..^".role.json".Length] : "")
        .Select(name => long.TryParse(name, out var id) ? id : 0)
        .Where(id => id > 0).Distinct().OrderBy(id => id);

    /// <summary>实体组构造时完成结构校验，仓库不重建业务模型。</summary>
    public static void Validate(RoleEntityTables role) => RoleEntityCache.Validate(role.Role.Value);
}
