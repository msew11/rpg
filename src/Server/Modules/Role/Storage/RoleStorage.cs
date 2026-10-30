using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using Rpg.Server.Modules.Common.Storage;
using Rpg.Server.Modules.Role.Data;

namespace Rpg.Server.Modules.Role.Storage;

/// <summary>角色三表存储；多表先记原始字节，用固定表名恢复未完成提交。</summary>
public sealed class RoleStorage : IRoleStorage
{
    /// <summary>宿主提供的 专用目录。</summary>
    private readonly string _directory;
    /// <summary>测试模拟进程在某张表替换后中断，不参与正式业务。</summary>
    internal Action<string>? AfterTableWritten { get; init; }
    /// <summary>绑定目录而不创建它；同一角色的调用由所属邮箱串行化。</summary>
    public RoleStorage(string directory) => _directory = Path.GetFullPath(directory);
    /// <summary>先恢复，再完整读取三张必需表；不增加业务校验。</summary>
    public async ValueTask<DataRoleSnapshot> LoadAsync(long roleId)
    {
        await RecoverAsync(roleId).ConfigureAwait(false);
        var role = await StorageJson.ReadAsync<DataRole>(GetPath(roleId, "role")).ConfigureAwait(false);
        var items = await StorageJson.ReadAsync<ImmutableArray<DataItem>>(GetPath(roleId, "item")).ConfigureAwait(false);
        var equipment = await StorageJson.ReadAsync<ImmutableArray<DataEquipment>>(GetPath(roleId, "equipment")).ConfigureAwait(false);
        return new(role, items, equipment);
    }
    /// <summary>恢复后逐个探测三表，只捕获明确不存在，权限及其他 IO 错误继续传播。</summary>
    public async ValueTask<DataRoleSnapshot?> TryLoadExistingAsync(long roleId)
    {
        await RecoverAsync(roleId).ConfigureAwait(false);
        var present = 0;
        foreach (var table in new[] { "role", "item", "equipment" })
        {
            try { await using var stream = File.OpenRead(GetPath(roleId, table)); present++; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (present == 0) return null;
        return await LoadAsync(roleId).ConfigureAwait(false);
    }
    /// <summary>提交变化表；失败留下回滚记录，下一次读写先恢复再继续。</summary>
    public ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables) => SaveCoreAsync(roleId, snapshot, tables, true);
    /// <summary>由专用地面批次拥有回滚记录，不再嵌套单角色记录。</summary>
    internal ValueTask SaveForItemsAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables) => SaveCoreAsync(roleId, snapshot, tables, false);
    /// <summary>按调用边界选择单角色或外层批次的提交点。</summary>
    private async ValueTask SaveCoreAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables, bool ownJournal)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await RecoverAsync(roleId).ConfigureAwait(false);
        var changes = new Dictionary<string, byte[]>();
        if (tables.HasFlag(DataRoleTables.Role)) changes.Add("role", StorageJson.Encode(snapshot.Role));
        if (tables.HasFlag(DataRoleTables.Item)) changes.Add("item", StorageJson.Encode(snapshot.Items));
        if (tables.HasFlag(DataRoleTables.Equipment)) changes.Add("equipment", StorageJson.Encode(snapshot.Equipments));
        if (changes.Count == 0) return;
        var pending = GetPath(roleId, "pending");
        if (ownJournal && changes.Count > 1)
        {
            var originals = new Dictionary<string, byte[]?>();
            foreach (var table in changes.Keys)
            {
                try { originals[table] = await File.ReadAllBytesAsync(GetPath(roleId, table)).ConfigureAwait(false); }
                catch (FileNotFoundException) { originals[table] = null; }
                catch (DirectoryNotFoundException) { originals[table] = null; }
            }
            await StorageJson.WriteAsync(pending, StorageJson.Encode(originals)).ConfigureAwait(false);
        }
        foreach (var (table, bytes) in changes)
        {
            await StorageJson.WriteAsync(GetPath(roleId, table), bytes).ConfigureAwait(false);
            AfterTableWritten?.Invoke(table);
        }
        if (ownJournal && changes.Count > 1) File.Delete(pending);
    }
    /// <summary>回滚原始字节，只有全部恢复成功才删除记录；非法元数据不得产生任意路径。</summary>
    private async ValueTask RecoverAsync(long roleId)
    {
        var pending = GetPath(roleId, "pending");
        Dictionary<string, byte[]?> originals;
        try { originals = await StorageJson.ReadAsync<Dictionary<string, byte[]?>>(pending).ConfigureAwait(false); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        if (originals.Count < 2 || originals.Keys.Any(key => key is not ("role" or "item" or "equipment")))
            throw new InvalidDataException("角色回滚记录包含非法表名或表集合");
        foreach (var (table, bytes) in originals)
        {
            var path = GetPath(roleId, table);
            if (bytes is null) File.Delete(path);
            else await StorageJson.WriteAsync(path, bytes).ConfigureAwait(false);
        }
        File.Delete(pending);
    }
    /// <summary>只从角色键与内部固定表名构造路径。</summary>
    private string GetPath(long roleId, string table) => Path.Combine(_directory, roleId.ToString(CultureInfo.InvariantCulture) + "." + table + ".json");
}
