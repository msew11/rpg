using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.World.Storage;

namespace Rpg.Server.Modules.Common.Storage;

/// <summary>地面物品专用的单批次共同落盘，只协调固定角色表、摘要及地图清单文件。</summary>
internal sealed class RepoItemBatch
{
    /// <summary>数据根。</summary>
    private readonly string _directory;
    /// <summary>固定角色三表存储。</summary>
    private readonly RoleStorage _roles;
    /// <summary>固定地图存储。</summary>
    private readonly WorldStorage _worlds;
    /// <summary>固定账号摘要存储。</summary>
    private readonly LoginStorage _logins;
    /// <summary>本实例唯一未完成批次记录。</summary>
    private string Pending => Path.Combine(_directory, "items.pending.json");
    /// <summary>验收在指定领域写完后模拟中断。</summary>
    internal Action<string>? AfterWrite { get; init; }
    /// <summary>借用同实例的具体存储，调用方必须持有物品入口。</summary>
    internal RepoItemBatch(string directory, RoleStorage roles, WorldStorage worlds, LoginStorage logins)
    { _directory = Path.GetFullPath(directory); _roles = roles; _worlds = worlds; _logins = logins; }
    /// <summary>启动业务前或失败重试前恢复整个未完成批次，全部成功才删除记录。</summary>
    internal async ValueTask RecoverAsync()
    {
        Dictionary<string, byte[]?> originals;
        try { originals = await StorageJson.ReadAsync<Dictionary<string, byte[]?>>(Pending).ConfigureAwait(false); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        // 先验证全部键，非法记录不能先恢复一部分文件。
        var paths = originals.Keys.ToDictionary(key => key, PathFor);
        foreach (var (key, bytes) in originals)
            if (bytes is null) File.Delete(paths[key]);
            else await StorageJson.WriteAsync(paths[key], bytes).ConfigureAwait(false);
        File.Delete(Pending);
    }
    /// <summary>候选已经捕获；完成标记删除前任一中断都可以恢复完整旧状态。</summary>
    internal async ValueTask SaveAsync(DataRoleSave[] roles, DataWorldSave[] worlds, DataLoginSave[] logins)
    {
        await RecoverAsync().ConfigureAwait(false);
        var paths = new List<string>();
        foreach (var role in roles)
        {
            if (role.Tables.HasFlag(DataRoleTables.Role)) paths.Add(FormattableString.Invariant($"{role.RoleId}.role.json"));
            if (role.Tables.HasFlag(DataRoleTables.Item)) paths.Add(FormattableString.Invariant($"{role.RoleId}.item.json"));
            if (role.Tables.HasFlag(DataRoleTables.Equipment)) paths.Add(FormattableString.Invariant($"{role.RoleId}.equipment.json"));
        }
        paths.AddRange(worlds.Select(world => FormattableString.Invariant($"maps/{world.Layout.MapId}/manifest.json")));
        paths.AddRange(logins.Select(login => UsernameKey.SummaryFileName(login.Username)));
        if (paths.Count == 0) return;
        var originals = new Dictionary<string, byte[]?>();
        foreach (var key in paths)
        {
            try { originals.Add(key, await File.ReadAllBytesAsync(PathFor(key)).ConfigureAwait(false)); }
            catch (FileNotFoundException) { originals.Add(key, null); }
            catch (DirectoryNotFoundException) { originals.Add(key, null); }
        }
        await StorageJson.WriteAsync(Pending, StorageJson.Encode(originals)).ConfigureAwait(false);
        foreach (var role in roles)
        { await _roles.SaveForItemsAsync(role.RoleId, role.Snapshot!, role.Tables).ConfigureAwait(false); AfterWrite?.Invoke("role"); }
        foreach (var world in worlds)
        { await _worlds.SaveForItemsAsync(world.Layout, world.Snapshot, world.Chunks).ConfigureAwait(false); AfterWrite?.Invoke("world"); }
        foreach (var login in logins)
        { await _logins.SaveAsync(login.Username, login.Summaries).ConfigureAwait(false); AfterWrite?.Invoke("login"); }
        File.Delete(Pending);
        foreach (var world in worlds) await _worlds.CleanupItemsAsync(world.Layout).ConfigureAwait(false);
    }
    /// <summary>恢复只允许本功能的固定文件结构，不接受任意相对路径。</summary>
    private string PathFor(string key)
    {
        var parts = key.Split('/');
        if (parts.Length == 3 && parts[0] == "maps" && parts[2] == "manifest.json" &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var map) && map > 0 && parts[1] == map.ToString(CultureInfo.InvariantCulture))
            return Path.Combine(_directory, "maps", parts[1], "manifest.json");
        if (parts.Length == 1)
        {
            var suffix = ".role_summary.json";
            if (key.EndsWith(suffix, StringComparison.Ordinal) && UsernameKey.SummaryFileName(key[..^suffix.Length]) == key)
                return Path.Combine(_directory, key);
            var table = key.Split('.');
            if (table.Length == 3 && table[2] == "json" && table[1] is "role" or "item" or "equipment" &&
                long.TryParse(table[0], NumberStyles.None, CultureInfo.InvariantCulture, out var role) && role > 0 && table[0] == role.ToString(CultureInfo.InvariantCulture))
                return Path.Combine(_directory, key);
        }
        throw new InvalidDataException("物品批次记录包含非法文件键");
    }
}
