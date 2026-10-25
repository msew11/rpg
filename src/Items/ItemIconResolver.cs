using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Rpg.Items;

/// <summary>背包与地面表现共用的图集解析器，只缓存资源目录。</summary>
internal sealed class ItemIconResolver
{
    /// <summary>按需要加载的资源身份索引，不保存道具业务数据。</summary>
    private Dictionary<string, DataItemAsset>? _assets;

    /// <summary>按配置素材键返回图集区域，缺图由调用方呈现占位。</summary>
    internal AtlasTexture? Resolve(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _assets ??= (JsonSerializer.Deserialize<DataItemAsset[]>(FileAccess.GetFileAsString("res://assets/items/catalog.json")) ?? [])
            .ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        if (!_assets.TryGetValue(id, out var asset) || !ResourceLoader.Exists(asset.Atlas)) return null;
        return new AtlasTexture { Atlas = GD.Load<Texture2D>(asset.Atlas), Region = new Rect2(asset.X, asset.Y, asset.W, asset.H), FilterClip = true };
    }
}
