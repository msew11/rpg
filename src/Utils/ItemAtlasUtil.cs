using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rpg.Utils;

/// <summary>读取项目内图集目录并按素材键裁剪纹理。</summary>
public static class ItemAtlasUtil
{
    /// <summary>读取素材清单；调用方持有自己所需的目录缓存。</summary>
    public static DataItemAsset[] LoadCatalog() =>
        JsonSerializer.Deserialize<DataItemAsset[]>(FileAccess.GetFileAsString("res://assets/items/catalog.json")) ?? [];

    /// <summary>以素材身份建立查找索引。</summary>
    public static Dictionary<string, DataItemAsset> Index(IEnumerable<DataItemAsset> catalog) =>
        catalog.ToDictionary(asset => asset.Id, StringComparer.Ordinal);

    /// <summary>从清单裁剪纹理，缺图时交由调用方呈现占位。</summary>
    public static AtlasTexture? Resolve(IReadOnlyDictionary<string, DataItemAsset> assets, string id) =>
        string.IsNullOrEmpty(id) || !assets.TryGetValue(id, out var asset) ? null : Texture(asset);

    /// <summary>按图集条目裁剪原像素纹理。</summary>
    public static AtlasTexture? Texture(DataItemAsset asset) => !ResourceLoader.Exists(asset.Atlas) ? null :
        new AtlasTexture { Atlas = GD.Load<Texture2D>(asset.Atlas),
            Region = new Rect2(asset.X, asset.Y, asset.W, asset.H), FilterClip = true };
}
