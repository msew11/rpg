using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Rpg.ServerV2.Modules.Common.Storage;
using Rpg.ServerV2.Modules.World.Data;

namespace Rpg.ServerV2.Modules.World.Storage;

/// <summary>以清单为唯一提交点的地图仓库；版本块先落地，不保存业务缓存。</summary>
public sealed class RepoWorld : IWorldRepo
{
    /// <summary>当前独立 V2 地图格式版本。</summary>
    private const int FormatVersion = 1;
    /// <summary>宿主提供的 V2 根目录。</summary>
    private readonly string _directory;
    /// <summary>提交后清理错误的诊断，不影响已提交结果。</summary>
    private readonly Action<Exception> _report;
    /// <summary>测试模拟候选写完而清单尚未提交时中断。</summary>
    internal Action? BeforeManifestCommit { get; init; }
    /// <summary>测试模拟提交后删除无引用块失败。</summary>
    internal Action<string>? BeforeChunkDelete { get; init; }
    /// <summary>绑定 V2 根目录，不预先创建文件或目录。</summary>
    public RepoWorld(string directory, Action<Exception>? report = null)
    { _directory = Path.GetFullPath(directory); _report = report ?? (static error => System.Diagnostics.Trace.WriteLine(error)); }
    /// <summary>只读取清单指向的块，全部成功后返回快照；孤立文件不参与加载。</summary>
    public async ValueTask<DataWorldSnapshot> LoadAsync(DataWorldLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var manifest = await ReadManifestAsync(layout).ConfigureAwait(false);
        if (manifest is null) return new([]);
        var objects = ImmutableArray.CreateBuilder<DataWorldObject>();
        foreach (var reference in manifest.Chunks)
        {
            var block = await StorageJson.ReadAsync<DataChunkFile>(ChunkPath(layout, reference)).ConfigureAwait(false);
            if (block.FormatVersion != FormatVersion || block.MapId != layout.MapId || block.X != reference.X ||
                block.Y != reference.Y || block.CommitId != reference.CommitId || block.Objects.IsDefault || block.Objects.Any(value => value is null))
                throw new InvalidDataException("V2 地图块结构或引用身份不一致");
            objects.AddRange(block.Objects);
        }
        return new(objects.ToImmutable());
    }
    /// <summary>先写所有候选块，最后替换清单；此前失败不影响旧清单和引用。</summary>
    public async ValueTask SaveAsync(DataWorldLayout layout, DataWorldSnapshot snapshot, ImmutableHashSet<DataWorldChunk> dirtyChunks)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(dirtyChunks);
        if (dirtyChunks.Count == 0) return;
        var previous = await ReadManifestAsync(layout).ConfigureAwait(false);
        var references = previous?.Chunks.ToDictionary(value => new DataWorldChunk(value.X, value.Y)) ?? new();
        var groups = layout.Group(snapshot.Objects);
        var commitId = Guid.NewGuid();
        foreach (var chunk in dirtyChunks.OrderBy(value => value.X).ThenBy(value => value.Y))
        {
            if (!groups.TryGetValue(chunk, out var objects)) { references.Remove(chunk); continue; }
            var reference = new DataChunkReference { X = chunk.X, Y = chunk.Y, CommitId = commitId };
            var block = new DataChunkFile { FormatVersion = FormatVersion, MapId = layout.MapId,
                X = chunk.X, Y = chunk.Y, CommitId = commitId, Objects = objects };
            await StorageJson.WriteAsync(ChunkPath(layout, reference), StorageJson.Encode(block)).ConfigureAwait(false);
            references[chunk] = reference;
        }
        var manifest = new DataManifest { FormatVersion = FormatVersion, MapId = layout.MapId, Layout = layout,
            CommitId = commitId, Chunks = references.Values.OrderBy(value => value.X).ThenBy(value => value.Y).ToArray() };
        BeforeManifestCommit?.Invoke();
        await StorageJson.WriteAsync(ManifestPath(layout), StorageJson.Encode(manifest)).ConfigureAwait(false);
        Cleanup(layout, manifest);
    }
    /// <summary>缺失清单是空地图；只捕获清单本身缺失，其他错误必须传播。</summary>
    private async ValueTask<DataManifest?> ReadManifestAsync(DataWorldLayout layout)
    {
        DataManifest manifest;
        try { manifest = await StorageJson.ReadAsync<DataManifest>(ManifestPath(layout)).ConfigureAwait(false); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        if (manifest.FormatVersion != FormatVersion || manifest.MapId != layout.MapId || manifest.Layout != layout ||
            manifest.CommitId == Guid.Empty || manifest.Chunks is null || manifest.Chunks.Any(value => value is null || value.CommitId == Guid.Empty) ||
            manifest.Chunks.Select(value => new DataWorldChunk(value.X, value.Y)).Distinct().Count() != manifest.Chunks.Length)
            throw new InvalidDataException("V2 地图清单结构、版本或布局不一致");
        return manifest;
    }
    /// <summary>提交后尽力删除规范无引用块；未知文件保留，诊断观察者也不能撤销提交。</summary>
    private void Cleanup(DataWorldLayout layout, DataManifest manifest)
    {
        try
        {
            var directory = Path.Combine(MapDirectory(layout), "chunks");
            if (!Directory.Exists(directory)) return;
            var live = manifest.Chunks.Select(FileName).ToHashSet(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(path);
                if (live.Contains(name) || !IsGeneratedFile(name)) continue;
                try { BeforeChunkDelete?.Invoke(path); File.Delete(path); }
                catch (Exception error) { Report(error); }
            }
        }
        catch (Exception error) { Report(error); }
    }
    /// <summary>严格识别本仓库的规范坐标和 GUID 文件名，不删除未知文件。</summary>
    private static bool IsGeneratedFile(string name)
    {
        var parts = name.Split('.');
        if (parts.Length != 3 || parts[2] != "json" || !Guid.TryParseExact(parts[1], "N", out var commitId)) return false;
        var coordinates = parts[0].Split('_');
        return coordinates.Length == 2 && int.TryParse(coordinates[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) &&
            int.TryParse(coordinates[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y) &&
            name == FileName(new() { X = x, Y = y, CommitId = commitId });
    }
    /// <summary>隔离诊断监听错误，提交成功必须正常返回。</summary>
    private void Report(Exception error)
    { try { _report(error); } catch (Exception observerError) { System.Diagnostics.Trace.WriteLine(observerError); } }
    /// <summary>从数值身份生成目录，不采纳存档中的任意路径。</summary>
    private string MapDirectory(DataWorldLayout layout) => Path.Combine(_directory, "maps", layout.MapId.ToString(CultureInfo.InvariantCulture));
    /// <summary>本地图唯一提交点。</summary>
    private string ManifestPath(DataWorldLayout layout) => Path.Combine(MapDirectory(layout), "manifest.json");
    /// <summary>只从数值块坐标和 GUID 提交编号生成规范文件名。</summary>
    private static string FileName(DataChunkReference reference) => FormattableString.Invariant($"{reference.X}_{reference.Y}.{reference.CommitId:N}.json");
    /// <summary>清单不保存路径，恢复时始终重新生成安全路径。</summary>
    private string ChunkPath(DataWorldLayout layout, DataChunkReference reference) => Path.Combine(MapDirectory(layout), "chunks", FileName(reference));
    /// <summary>文件结构专用清单，不对外暴露可变元数据。</summary>
    private sealed class DataManifest
    {
        /// <summary>格式版本。</summary>
        [JsonRequired] public int FormatVersion { get; init; }
        /// <summary>固定地图身份。</summary>
        [JsonRequired] public int MapId { get; init; }
        /// <summary>生成存档时的完整规格。</summary>
        [JsonRequired] public DataWorldLayout Layout { get; init; } = null!;
        /// <summary>最近完整提交编号。</summary>
        [JsonRequired] public Guid CommitId { get; init; }
        /// <summary>只包含有效非空块的引用。</summary>
        [JsonRequired] public DataChunkReference[] Chunks { get; init; } = null!;
    }
    /// <summary>安全引用只保存坐标与提交编号，不提供路径字段。</summary>
    private sealed class DataChunkReference
    {
        /// <summary>块横坐标。</summary>
        [JsonRequired] public int X { get; init; }
        /// <summary>块纵坐标。</summary>
        [JsonRequired] public int Y { get; init; }
        /// <summary>该块内容所属提交编号。</summary>
        [JsonRequired] public Guid CommitId { get; init; }
    }
    /// <summary>带结构身份的不可变对象块文件。</summary>
    private sealed class DataChunkFile
    {
        /// <summary>格式版本。</summary>
        [JsonRequired] public int FormatVersion { get; init; }
        /// <summary>固定地图身份。</summary>
        [JsonRequired] public int MapId { get; init; }
        /// <summary>块横坐标。</summary>
        [JsonRequired] public int X { get; init; }
        /// <summary>块纵坐标。</summary>
        [JsonRequired] public int Y { get; init; }
        /// <summary>该块所属提交编号。</summary>
        [JsonRequired] public Guid CommitId { get; init; }
        /// <summary>完整块内对象，不执行对象业务校验。</summary>
        [JsonRequired] public ImmutableArray<DataWorldObject> Objects { get; init; }
    }
}
