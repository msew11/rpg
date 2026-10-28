using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server;

/// <summary>无业务缓存的角色文件仓库，按变化表原子提交并恢复中断事务。</summary>
public sealed class RepoServerRole
{
    /// <summary>成功组提交后的诊断日志。</summary>
    private readonly Action<string, string>? _writeLog;
    /// <summary>实体文件根目录。</summary>
    private readonly string _directory;
    /// <summary>实体表使用与配置一致的 camelCase 对象或无包装数组。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>绑定实体目录和成功提交日志，调用写入方法即执行文件提交。</summary>
    public RepoServerRole(string directory, Action<string, string>? writeLog = null)
    {
        _directory = Path.GetFullPath(directory);
        _writeLog = writeLog;
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
        Recover(roleId);
        var role = Read(roleId);
        if (role.Role.Value.IsDeleted) throw new InvalidDataException("角色已删除");
        return role;
    }

    /// <summary>按需加载完整角色列表；登录及角色管理只读取 ListSummaries。</summary>
    public IReadOnlyList<RoleEntityTables> List() => ListSummaries().Select(value => Load(value.RoleId)).ToList().AsReadOnly();

    /// <summary>首次只读取全部摘要，不通过完整角色数据回填。</summary>
    public IReadOnlyList<RoleSummaryEntity> ListSummaries()
    {
        return ReadSummaries().Where(value => !value.IsDeleted).ToList().AsReadOnly();
    }

    /// <summary>读取全部摘要及墓碑，完整校验后返回独立快照。</summary>
    internal IReadOnlyList<RoleSummaryEntity> ReadSummaries()
    {
        if (!Directory.Exists(_directory)) return [];
        foreach (var path in Directory.EnumerateFiles(_directory, "*.pending.json").ToArray())
        {
            var prefix = Path.GetFileName(path)[..^".pending.json".Length];
            if (long.TryParse(prefix, out var id) && id > 0) Recover(id);
        }
        var values = new List<RoleSummaryEntity>();
        foreach (var id in StoredIds())
        {
            var value = ReadJson<RoleSummaryEntity>(GetSummaryPath(id), JsonOptions);
            ValidateSummary(value, id);
            values.Add(value);
        }
        return values.AsReadOnly();
    }

    /// <summary>完整保存只提交发生变化的实体文件，创建时提交全部表。</summary>
    public void Save(RoleEntityTables role)
    {
        Validate(role);
        Recover(role.Role.Value.RoleId);
        Commit(role.Role.Value.RoleId, Changed(role.Role.Value.RoleId, Serialize(role)));
    }

    /// <summary>移动只写角色单例，展示字段变化时同时更新摘要。</summary>
    public void SaveRole(RoleEntityTables role)
    {
        Validate(role);
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
        Recover(role.Role.Value.RoleId);
        var values = Serialize(role);
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

    /// <summary>生成固定领域路径，拒绝无效身份。</summary>
    private string GetPath(long roleId, string domain)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        return Path.Combine(_directory, $"{roleId}.{domain}.json");
    }

    /// <summary>读取当前四张实体表，交由主键缓存和组合候选校验。</summary>
    private RoleEntityTables Read(long roleId)
    {
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
        var context = new ServerRequestContext();
        foreach (var summary in ReadSummaries()) context.Login.Summaries.Add(summary.RoleId, summary);
        context.Login.Loaded = true;
        var role = Handler.Role.RoleCreation.Create(context, name, gender, headId, mapId, spawnX, spawnY);
        Save(role);
        return role;
    }

    /// <summary>只读取角色单例，供未选角色删除及独立仓库验收使用。</summary>
    internal RoleEntity ReadRole(long roleId) { Recover(roleId); return ReadRoleEntity(roleId); }

    /// <summary>原子提交角色单例与摘要墓碑，不加载物品或装备。</summary>
    internal void SaveDeletion(RoleEntity role, RoleSummaryEntity summary) => Commit(role.RoleId,
        Changed(role.RoleId, new Dictionary<string, string>
        {
            ["role"] = JsonSerializer.Serialize(role, JsonOptions),
            ["role_summary"] = JsonSerializer.Serialize(summary, JsonOptions)
        }));

    /// <summary>独立仓库显式删除，运行时由登录 Actor 暂存墓碑后调用提交。</summary>
    public void Delete(long roleId)
    {
        var summary = ReadSummaries().SingleOrDefault(value => value.RoleId == roleId && !value.IsDeleted)
            ?? throw new InvalidDataException("角色不存在或已删除");
        SaveDeletion(ReadRole(roleId) with { IsDeleted = true }, summary with { IsDeleted = true });
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
