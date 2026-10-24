using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using FileAccess = Godot.FileAccess;
using Json.Schema;
using Rpg.Config.Data;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>扫描、校验并组合打包的 JSON 游戏配置。</summary>
public sealed class ConfigService : Service
{
    /// <summary>通过 Godot 从环境指定的目录加载配置及配套校验规则。</summary>
    public ConfigService(EnvService env)
    {
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        Load(env.ConfigDirectory, env.SchemaDirectory);
    }

    /// <summary>最近一次成功加载的地图配置。</summary>
    private MapConfig? _mapConfig;

    /// <summary>已加载的道具表，按唯一编号索引。</summary>
    private IReadOnlyDictionary<int, ItemCfg>? _itemCfgs;

    /// <summary>已校验的只读道具映射。</summary>
    public IReadOnlyDictionary<int, ItemCfg> ItemCfgs
    {
        get
        {
            ThrowIfDisposed();
            return _itemCfgs ?? throw new InvalidOperationException("道具配置尚未加载");
        }
    }

    /// <summary>已校验的地图配置；首次成功加载前不可读取。</summary>
    public MapConfig MapConfig
    {
        get
        {
            ThrowIfDisposed();
            return _mapConfig ?? throw new InvalidOperationException("地图配置尚未加载");
        }
    }

    /// <summary>从 Godot 资源包递归读取配置，支持编辑器和导出程序。</summary>
    private void Load(string configDirectory, string schemaDirectory)
    {
        ThrowIfDisposed();
        var files = new List<(string Path, string Text)>();
        Scan(configDirectory, files);
        var schemas = new Dictionary<string, JsonSchema>();
        foreach (var kind in new[] { "map", "item" })
        {
            var path = $"{schemaDirectory}/{kind}.schema.json";
            var text = FileAccess.GetFileAsString(path);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException($"Schema 不存在或为空：{path}");
            schemas.Add(kind, JsonSchema.FromText(text));
        }
        (_mapConfig, _itemCfgs) = LoadDocuments(files, schemas);
    }

    /// <summary>递归读取资源包中所有 JSON 文件。</summary>
    private static void Scan(string directory, List<(string Path, string Text)> files)
    {
        using var dir = DirAccess.Open(directory)
            ?? throw new InvalidDataException($"配置目录不存在：{directory}");
        dir.ListDirBegin();
        while (true)
        {
            var name = dir.GetNext();
            if (name.Length == 0) break;
            if (name.StartsWith('.')) continue;
            var path = $"{directory}/{name}";
            if (dir.CurrentIsDir()) Scan(path, files);
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                files.Add((path, FileAccess.GetFileAsString(path)));
        }
        dir.ListDirEnd();
    }

    /// <summary>校验已读取的全部配置文档并组合类型化数据。</summary>
    private static (MapConfig Map, IReadOnlyDictionary<int, ItemCfg> Items) LoadDocuments(
        IEnumerable<(string Path, string Text)> files, IReadOnlyDictionary<string, JsonSchema> schemas)
    {
        MapConfig? map = null;
        Dictionary<int, ItemCfg>? items = null;
        var documents = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        if (documents.Length == 0) throw new InvalidDataException("配置目录没有 JSON 文件");

        foreach (var file in documents)
        {
            try
            {
                var document = JsonNode.Parse(file.Text) as JsonObject
                    ?? throw new InvalidDataException("JSON 配置必须为对象");
                var kind = document["kind"]?.GetValue<string>()
                    ?? throw new InvalidDataException("缺少 kind 字段");
                if (!schemas.TryGetValue(kind, out var schema))
                    throw new InvalidDataException($"未知配置类型：{kind}");
                if (System.IO.Path.GetFileName(file.Path) != $"{kind}.json")
                    throw new InvalidDataException($"配置文件名必须为 {kind}.json");
                if (kind == "map" && map is not null)
                    throw new InvalidDataException("重复的地图配置");
                if (kind == "item" && items is not null)
                    throw new InvalidDataException("重复的道具配置");

                var result = schema.Evaluate(JsonSerializer.SerializeToElement(document),
                    new EvaluationOptions { OutputFormat = OutputFormat.List });
                if (!result.IsValid)
                    throw new InvalidDataException($"Schema 校验失败：{JsonSerializer.Serialize(result)}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                if (kind == "item")
                {
                    var rows = document["items"]!.Deserialize<ItemCfg[]>(options)
                        ?? throw new InvalidDataException("道具配置无法解码");
                    items = new Dictionary<int, ItemCfg>();
                    foreach (var row in rows)
                        if (!items.TryAdd(row.Id, row))
                            throw new InvalidDataException($"重复的道具 ID：{row.Id}");
                    continue;
                }

                map = document.Deserialize<MapConfig>(options)
                    ?? throw new InvalidDataException("地图配置无法解码");

                if (map.Overrides.Any(tile => tile.X >= map.Width || tile.Y >= map.Height))
                    throw new InvalidDataException("覆盖格超出地图边界");
                if (map.Overrides.GroupBy(tile => (tile.X, tile.Y)).Any(group => group.Count() > 1))
                    throw new InvalidDataException("覆盖格坐标重复");
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or IOException or InvalidOperationException)
            {
                throw new InvalidDataException($"配置文件 {file.Path} 无效：{error.Message}", error);
            }
        }

        if (map is null) throw new InvalidDataException("缺少地图配置 map.json");
        if (items is null) throw new InvalidDataException("缺少道具配置 item.json");
        return (map, new ReadOnlyDictionary<int, ItemCfg>(items));
    }
    /// <summary>释放本次启动的配置状态。</summary>
    public override void Dispose() { IsDisposed = true; _mapConfig = null; _itemCfgs = null; }

}
