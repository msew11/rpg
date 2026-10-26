using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Rpg.Services;

namespace Rpg.Utils;

/// <summary>在一次客户端启动内共享物品目录、图集和裁剪纹理，主线程按需加载。</summary>
public sealed class ItemAssetService : Service
{
    /// <summary>首次使用后共享的原序只读目录。</summary>
    private IReadOnlyList<DataItemAsset>? _catalog;
    /// <summary>素材键到同一目录条目的只读索引。</summary>
    private IReadOnlyDictionary<string, DataItemAsset>? _index;
    /// <summary>强引用已使用图集，空值记住缺图结果。</summary>
    private readonly Dictionary<string, Texture2D?> _atlases = new(StringComparer.Ordinal);
    /// <summary>按素材复用裁剪资源，空值保留缺图占位。</summary>
    private readonly Dictionary<string, AtlasTexture?> _textures = new(StringComparer.Ordinal);
    /// <summary>用于验收一次启动不重复解析目录。</summary>
    internal int CatalogLoadCount { get; private set; }
    /// <summary>用于验收仅按需要加载图集。</summary>
    internal int LoadedAtlasCount => _atlases.Count;
    /// <summary>用于验收开包只读取已准备纹理。</summary>
    internal int CachedTextureCount => _textures.Count;
    /// <summary>目录读取、解析和索引的累计主线程耗时。</summary>
    internal double CatalogLoadMilliseconds { get; private set; }
    /// <summary>图集加载和裁剪资源创建的累计主线程耗时。</summary>
    internal double TextureLoadMilliseconds { get; private set; }

    /// <summary>创建空缓存，不在登录前主动加载图集。</summary>
    public ItemAssetService() { }

    /// <summary>为缺失图集验收提供独立目录，不改写项目资源。</summary>
    internal ItemAssetService(IEnumerable<DataItemAsset> catalog) => SetCatalog(catalog.ToArray());

    /// <summary>共享原序目录，集合和条目均不可修改。</summary>
    public IReadOnlyList<DataItemAsset> Catalog
    {
        get { EnsureCatalog(); return _catalog!; }
    }

    /// <summary>共享素材键索引，供图鉴查找候选图片。</summary>
    public IReadOnlyDictionary<string, DataItemAsset> Index
    {
        get { EnsureCatalog(); return _index!; }
    }

    /// <summary>准备当前角色图标，同素材和同源图集自动复用。</summary>
    public void Preload(IEnumerable<string> ids)
    {
        EnsureCatalog();
        foreach (var id in ids.Distinct(StringComparer.Ordinal)) Resolve(id);
    }

    /// <summary>返回共享裁剪纹理，未知键和缺失图集返回空值供现有占位使用。</summary>
    public AtlasTexture? Resolve(string id)
    {
        EnsureCatalog();
        if (string.IsNullOrEmpty(id)) return null;
        if (_textures.TryGetValue(id, out var texture)) return texture;
        if (!_index!.TryGetValue(id, out var asset)) return _textures[id] = null;
        var start = Stopwatch.GetTimestamp();
        if (!_atlases.TryGetValue(asset.Atlas, out var atlas))
        {
            atlas = ResourceLoader.Exists(asset.Atlas) ? GD.Load<Texture2D>(asset.Atlas) : null;
            _atlases.Add(asset.Atlas, atlas);
        }
        texture = atlas is null ? null : new AtlasTexture { Atlas = atlas,
            Region = new Rect2(asset.X, asset.Y, asset.W, asset.H), FilterClip = true };
        _textures.Add(id, texture);
        TextureLoadMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return texture;
    }

    /// <summary>完整读取和建立索引后才发布，失败不遗留部分目录。</summary>
    private void EnsureCatalog()
    {
        ThrowIfDisposed();
        if (_catalog is not null) return;
        var start = Stopwatch.GetTimestamp();
        SetCatalog(ItemAtlasUtil.LoadCatalog());
        CatalogLoadCount++;
        CatalogLoadMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>用不可变条目建立只读集合，目录和索引共享条目。</summary>
    private void SetCatalog(DataItemAsset[] catalog)
    {
        var index = ItemAtlasUtil.Index(catalog);
        _catalog = Array.AsReadOnly(catalog);
        _index = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DataItemAsset>(index);
    }

    /// <summary>清空本服务的资源引用，仍被节点引用的纹理由 Godot 管理。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _textures.Clear();
        _atlases.Clear();
        _index = null;
        _catalog = null;
    }
}
