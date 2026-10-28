using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpg.Server;

/// <summary>物品转移专用文件事务，只保存固定领域的旧文本，不拥有业务状态。</summary>
internal sealed class RepoServerItemTransfer
{
    /// <summary>所有路径的固定服务端根目录。</summary>
    private readonly string _root;
    /// <summary>成功组提交后才输出的诊断回调。</summary>
    private readonly Action<string, string>? _log;
    /// <summary>同一时刻只存在一个同步提交组。</summary>
    internal string PendingPath => Path.Combine(_root, "item-transfer.pending.json");
    /// <summary>仅验收使用的最后提交点前故障注入。</summary>
    internal Action? BeforeComplete { get; set; }

    /// <summary>绑定专用目录，不持有角色、地图或网络模块。</summary>
    internal RepoServerItemTransfer(string root, Action<string, string>? log = null)
    {
        _root = Path.GetFullPath(root);
        _log = log;
    }

    /// <summary>写旧文件快照，调用方在此之前不能修改任何业务文件。</summary>
    internal void Begin(int mapId, IEnumerable<long> roles)
    {
        if (File.Exists(PendingPath)) throw new IOException("尚有未恢复的物品转移记录");
        var ids = roles.OrderBy(id => id).ToArray();
        var paths = Paths(mapId, ids);
        var originals = paths.ToDictionary(path => path, path =>
            File.Exists(Path.Combine(_root, path)) ? File.ReadAllText(Path.Combine(_root, path)) : null);
        WriteAtomic(PendingPath, JsonSerializer.Serialize(new DataTransferFile(2, mapId, ids, originals)));
    }

    /// <summary>删除记录是组提交点，失败时仍可恢复旧组。</summary>
    internal void Complete()
    {
        BeforeComplete?.Invoke();
        File.Delete(PendingPath);
    }

    /// <summary>在任何业务读取前幂等恢复，全部成功才移除记录，失败不掩盖损坏。</summary>
    internal void Recover()
    {
        if (!File.Exists(PendingPath)) return;
        try
        {
            var data = JsonSerializer.Deserialize<DataTransferFile>(File.ReadAllText(PendingPath))
                ?? throw new InvalidDataException("转移记录为空");
            if (data.FormatVersion != 2 || data.Roles is null || data.Originals is null)
                throw new InvalidDataException("转移记录格式无效");
            var paths = Paths(data.MapId, data.Roles);
            if (!paths.ToHashSet(StringComparer.Ordinal).SetEquals(data.Originals.Keys))
                throw new InvalidDataException("转移记录包含非法文件路径");
            foreach (var relative in paths)
            {
                var path = Path.Combine(_root, relative);
                var text = data.Originals[relative];
                if (text is null) { if (File.Exists(path)) File.Delete(path); }
                else if (!File.Exists(path) || File.ReadAllText(path) != text) WriteAtomic(path, text);
            }
            File.Delete(PendingPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
        {
            throw new IOException($"无法恢复物品转移：{PendingPath}；{error.Message}", error);
        }
    }

    /// <summary>成功后输出实际提交内容，诊断异常不改变事务结果。</summary>
    internal void LogCommitted(IEnumerable<(string Path, string Text)> files)
    {
        foreach (var file in files)
            try { _log?.Invoke(file.Path, file.Text); }
            catch (Exception error) { System.Diagnostics.Trace.WriteLine(error); }
    }

    /// <summary>只从正整数身份生成固定文件名，拒绝任意路径和重复角色。</summary>
    private static string[] Paths(int mapId, long[] roles)
    {
        if (mapId <= 0 || roles.Length == 0 || roles.Any(id => id <= 0) || roles.Distinct().Count() != roles.Length)
            throw new InvalidDataException("转移记录地图或角色身份无效");
        return roles.SelectMany(id => new[] { $"{id}.role.json", $"{id}.item.json", $"{id}.equipment.json", $"{id}.role_summary.json", $"{id}.pending.json" })
            .Append(Path.Combine("maps", mapId.ToString(System.Globalization.CultureInfo.InvariantCulture), "manifest.json")).ToArray();
    }

    /// <summary>同目录写临时文件并刷盘后替换，不输出候选日志。</summary>
    private static void WriteAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Encoding.UTF8.GetBytes(content));
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>专用回滚记录，旧块由地图仓库保留，不复制整图。</summary>
    /// <param name="FormatVersion">记录格式。</param>
    /// <param name="MapId">参与地图。</param>
    /// <param name="Roles">参与角色。</param>
    /// <param name="Originals">固定领域的旧文本，缺失为 null。</param>
    private sealed record DataTransferFile(
        [property: JsonRequired] int FormatVersion,
        [property: JsonRequired] int MapId,
        [property: JsonRequired] long[] Roles,
        [property: JsonRequired] Dictionary<string, string?> Originals);
}
