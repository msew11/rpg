using Rpg.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-item-assets 的共享资源、按需加载、缺图和释放重建契约。</summary>
[Trait("Runtime", "Godot")]
public sealed class ItemAssetTests
{
    /// <summary>空包只准备目录；后续角色按需补充图集，同图集的素材共用纹理源。</summary>
    [Fact]
    public void EmptyPreloadAndLaterRolesReuseCatalogAndOnlyNeededAtlases()
    {
        using var service = new ItemAssetService();
        service.Preload([]);
        Assert.Equal(0, service.LoadedAtlasCount);
        var catalog = service.Catalog;
        var group = catalog.GroupBy(entry => entry.Atlas).First(entries => entries.Count() >= 2);
        var entries = group.Take(2).ToArray();
        var other = catalog.First(entry => entry.Atlas != group.Key);
        service.Preload([entries[0].Id, entries[0].Id, entries[1].Id]);
        var first = service.Resolve(entries[0].Id);
        Assert.NotNull(first);
        Assert.Same(first, service.Resolve(entries[0].Id));
        Assert.Same(first.Atlas, service.Resolve(entries[1].Id)!.Atlas);
        Assert.Equal(1, service.LoadedAtlasCount);
        service.Preload([other.Id]);
        Assert.NotNull(service.Resolve(other.Id));
        Assert.Equal(2, service.LoadedAtlasCount);
        Assert.Same(catalog, service.Catalog);
        Assert.Same(entries[0], service.Index[entries[0].Id]);
        Assert.Equal(1, service.CatalogLoadCount);
    }

    /// <summary>未知键与缺图返回空纹理，多次查询保持占位且不尝试无关资源。</summary>
    [Fact]
    public void MissingAssetsRemainPlaceholders()
    {
        using var service = new ItemAssetService([new DataItemAsset { Id = "missing",
            Atlas = "res://assets/items/no-such-atlas.png", W = 32, H = 32 }]);
        service.Preload(["", "unknown", "missing", "missing"]);
        Assert.Null(service.Resolve(""));
        Assert.Null(service.Resolve("unknown"));
        Assert.Null(service.Resolve("missing"));
        Assert.Equal(1, service.LoadedAtlasCount);
    }

    /// <summary>释放后清空持有的资源并拒绝访问，重新创建得到独立目录与裁剪纹理。</summary>
    [Fact]
    public void DisposalClearsResourcesAndNewServiceStartsIndependently()
    {
        var service = new ItemAssetService();
        var catalog = service.Catalog;
        var texture = service.Resolve(catalog[0].Id);
        Assert.NotNull(texture);
        service.Dispose();
        service.Dispose();
        Assert.Equal(0, service.LoadedAtlasCount);
        Assert.Equal(0, service.CachedTextureCount);
        Assert.Throws<ObjectDisposedException>(() => service.Catalog);
        Assert.Throws<ObjectDisposedException>(() => service.Index);
        Assert.Throws<ObjectDisposedException>(() => service.Resolve(""));
        Assert.Throws<ObjectDisposedException>(() => service.Preload([]));
        using var next = new ItemAssetService();
        Assert.NotSame(catalog, next.Catalog);
        Assert.NotSame(texture, next.Resolve(catalog[0].Id));
        Assert.Equal(1, next.CatalogLoadCount);
    }
}
