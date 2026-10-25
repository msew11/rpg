using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>地图文件边界：不可变块版本与清单原子提交，不拥有业务对象表。</summary>
public sealed class RepoServerMap
{
    /// <summary>服务端根目录，地图按正整数身份隔离。</summary>
    private readonly string _root;
    /// <summary>成功业务提交的日志回调，诊断故障不改变业务结果。</summary>
    private readonly Action<string, string>? _writeLog;
    /// <summary>仅测试使用的清单替换前故障点，不参与业务拓扑。</summary>
    internal Action? BeforeManifestCommit { get; init; }
    /// <summary>统一使用可阅读的驼峰文件格式。</summary>
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    /// <summary>只识别本仓库生成的块版本文件，不删除未知文件。</summary>
    private static readonly Regex ChunkName = new(@"^\d+_\d+\.[0-9a-f]{32}\.json$", RegexOptions.CultureInvariant);

    /// <summary>绑定文件目录及诊断输出，不接受配置或网络服务。</summary>
    public RepoServerMap(string directory, Action<string, string>? writeLog = null)
    {
        _root = Path.GetFullPath(directory);
        _writeLog = writeLog;
    }

    /// <summary>返回该地图的唯一提交入口。</summary>
    public string ManifestPath(int mapId)
    {
        if (mapId <= 0) throw new InvalidDataException("地图 ID 必须为正整数");
        return Path.Combine(_root, "maps", mapId.ToString(CultureInfo.InvariantCulture), "manifest.json");
    }

    /// <summary>只恢复清单明确引用的块，校验完成前不清理任何原文件。</summary>
    public IReadOnlyList<DataServerMapObject> Load(DataServerMapRules rules)
    {
        var manifest = ReadManifest(rules);
        var result = new List<DataServerMapObject>();
        var ids = new HashSet<Guid>();
        foreach (var entry in manifest.Chunks)
        {
            var file = ReadChunk(rules, entry);
            foreach (var value in file.Objects)
            {
                if (value is null) throw new InvalidDataException("地图块包含空对象");
                value.Validate();
                if (rules.ChunkAt(value.X, value.Y) != (entry.X, entry.Y) || !ids.Add(value.ObjectId))
                    throw new InvalidDataException($"地图 {rules.MapId} 的块 {entry.File} 归属错误或对象重复");
                result.Add(value);
            }
        }
        return result;
    }

    /// <summary>仅写变化块，以一次清单替换提交全部变化；失败不覆盖旧提交。</summary>
    public IReadOnlyList<(string Path, string Text)> Commit(DataServerMapRules rules, IReadOnlyDictionary<(int X, int Y), IReadOnlyList<DataServerMapObject>> changes,
        bool cleanup = true, bool log = true)
    {
        var manifest = ReadManifest(rules);
        if (changes.Count == 0) { if (cleanup) Cleanup(rules, manifest); return []; }
        var entries = manifest.Chunks.ToDictionary(entry => (entry.X, entry.Y));
        var commitId = Guid.NewGuid().ToString("N");
        var committedFiles = new List<(string Path, string Text)>();
        foreach (var (key, values) in changes.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            if (entries.TryGetValue(key, out var prior) && ReadChunk(rules, prior).Objects.SequenceEqual(values)) continue;
            if (values.Count == 0) { entries.Remove(key); continue; }
            var name = $"{key.X}_{key.Y}.{commitId}.json";
            var path = Path.Combine(Path.GetDirectoryName(ManifestPath(rules.MapId))!, "chunks", name);
            var text = JsonSerializer.Serialize(new DataChunkFile(1, rules.MapId, key.X, key.Y, commitId, values.ToArray()), Json);
            WriteAtomic(path, text, replace: false);
            committedFiles.Add((path, text));
            entries[key] = new(key.X, key.Y, name);
        }
        var updated = entries.Values.OrderBy(entry => entry.Y).ThenBy(entry => entry.X).ToArray();
        if (manifest.Chunks.SequenceEqual(updated)) { if (cleanup) Cleanup(rules, manifest); return []; }
        var candidate = new DataManifest(1, rules.MapId, rules, commitId, updated);
        var content = JsonSerializer.Serialize(candidate, Json);
        BeforeManifestCommit?.Invoke();
        WriteAtomic(ManifestPath(rules.MapId), content, replace: true);
        committedFiles.Add((ManifestPath(rules.MapId), content));
        if (log) foreach (var file in committedFiles) Log(file.Path, file.Text);
        if (cleanup) Cleanup(rules, candidate);
        return committedFiles;
    }

    /// <summary>缺失清单是空地图，已有清单必须完整匹配版本和布局。</summary>
    private DataManifest ReadManifest(DataServerMapRules rules)
    {
        rules.Validate();
        var path = ManifestPath(rules.MapId);
        if (!File.Exists(path)) return new(1, rules.MapId, rules, "", []);
        var data = Read<DataManifest>(path);
        if (data.FormatVersion != 1 || data.MapId != rules.MapId || data.Layout != rules ||
            !Guid.TryParseExact(data.CommitId, "N", out var id) || id == Guid.Empty || data.Chunks is null)
            throw new InvalidDataException($"地图清单版本、身份或布局无效：{path}");
        var seen = new HashSet<(int, int)>();
        foreach (var entry in data.Chunks)
        {
            if (entry is null || !seen.Add((entry.X, entry.Y)) || !ChunkName.IsMatch(entry.File ?? "") ||
                !entry.File!.StartsWith($"{entry.X}_{entry.Y}.", StringComparison.Ordinal))
                throw new InvalidDataException($"地图清单引用无效：{path}");
            _ = rules.Neighborhood(entry.X, entry.Y);
        }
        return data;
    }

    /// <summary>文件名只从已校验的清单读取，内容必须匹配身份及所属块。</summary>
    private DataChunkFile ReadChunk(DataServerMapRules rules, DataChunkEntry entry)
    {
        var path = Path.Combine(Path.GetDirectoryName(ManifestPath(rules.MapId))!, "chunks", entry.File);
        var data = Read<DataChunkFile>(path);
        if (data.FormatVersion != 1 || data.MapId != rules.MapId || data.ChunkX != entry.X || data.ChunkY != entry.Y ||
            data.Objects is null || data.Objects.Length == 0 || entry.File != $"{entry.X}_{entry.Y}.{data.CommitId}.json")
            throw new InvalidDataException($"地图块内容无效：{path}");
        return data;
    }

    /// <summary>统一包装文件路径，损坏或缺失不得回退为空。</summary>
    private static T Read<T>(string path) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("文件内容为空"); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        { throw new InvalidDataException($"无法读取地图存档：{path}", error); }
    }

    /// <summary>同目录刷盘后原子替换，候选文件永不覆盖已存在块版本。</summary>
    private static void WriteAtomic(string path, string content, bool replace)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (replace && File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>清理只触及本地图已不再引用的版本；失败留待下个检测节点。</summary>
    private void Cleanup(DataServerMapRules rules, DataManifest manifest)
    {
        var directory = Path.Combine(Path.GetDirectoryName(ManifestPath(rules.MapId))!, "chunks");
        if (!Directory.Exists(directory)) return;
        var referenced = manifest.Chunks.Select(entry => entry.File).ToHashSet(StringComparer.Ordinal);
        var errors = new List<Exception>();
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);
            if (!ChunkName.IsMatch(name) || referenced.Contains(name)) continue;
            try { File.Delete(path); }
            catch (IOException error) { errors.Add(error); }
            catch (UnauthorizedAccessException error) { errors.Add(error); }
        }
        if (errors.Count != 0) throw new AggregateException("地图提交已完成，旧版本清理失败，后续检测重试", errors);
    }

    /// <summary>日志不参与提交成功与否的判断。</summary>
    private void Log(string path, string text)
    {
        try { _writeLog?.Invoke(path, text); }
        catch { /* 诊断失败不撤销已经原子提交的地图。 */ }
    }

    /// <summary>清单中的一个有效块引用。</summary>
    /// <param name="X">横向块坐标。</param>
    /// <param name="Y">纵向块坐标。</param>
    /// <param name="File">块版本文件名。</param>
    private sealed record DataChunkEntry(int X, int Y, string File);
    /// <summary>整张地图的持久化提交入口。</summary>
    /// <param name="FormatVersion">存档格式版本。</param>
    /// <param name="MapId">地图身份。</param>
    /// <param name="Layout">恢复时校验的地图布局。</param>
    /// <param name="CommitId">本次清单提交标识。</param>
    /// <param name="Chunks">有效非空块的引用集合。</param>
    private sealed record DataManifest(int FormatVersion, int MapId, DataServerMapRules Layout, string CommitId, DataChunkEntry[] Chunks);
    /// <summary>不可变块版本的文件载荷。</summary>
    /// <param name="FormatVersion">存档格式版本。</param>
    /// <param name="MapId">地图身份。</param>
    /// <param name="ChunkX">横向块坐标。</param>
    /// <param name="ChunkY">纵向块坐标。</param>
    /// <param name="CommitId">生成块文件的提交标识。</param>
    /// <param name="Objects">该块完整对象快照。</param>
    private sealed record DataChunkFile(int FormatVersion, int MapId, int ChunkX, int ChunkY, string CommitId, DataServerMapObject[] Objects);
}
