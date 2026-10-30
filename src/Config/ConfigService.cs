using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Rpg.Config.Cache;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>持有具体配置缓存，并按文件标注统一加载全部客户端配置。</summary>
public sealed class ConfigService : Service
{
    /// <summary>本次启动拥有的多例地图缓存。</summary>
    private readonly MapCfgCache _map;
    /// <summary>本次启动拥有的多例道具缓存。</summary>
    private readonly ItemCfgCache _item;
    /// <summary>本次启动拥有的单例基础参数缓存。</summary>
    private readonly BasicCfgCache _basic;
    /// <summary>本次启动拥有的独立装备配置缓存。</summary>
    private readonly EquipmentCfgCache _equipment;
    /// <summary>供统一加载和释放使用的缓存集合。</summary>
    private readonly CfgCache[] _caches;

    /// <summary>已完成加载的多例地图缓存。</summary>
    public MapCfgCache Map
    {
        get { ThrowIfDisposed(); return _map; }
    }

    /// <summary>已完成加载的多例道具缓存。</summary>
    public ItemCfgCache Item
    {
        get { ThrowIfDisposed(); return _item; }
    }

    /// <summary>已完成加载的基础单例缓存。</summary>
    public BasicCfgCache Basic
    {
        get { ThrowIfDisposed(); return _basic; }
    }

    /// <summary>已完成加载及关联检测的装备配置。</summary>
    public EquipmentCfgCache Equipment
    {
        get { ThrowIfDisposed(); return _equipment; }
    }

    /// <summary>显式建立具体缓存，统一加载；任一失败都回收已创建缓存并抛出异常。</summary>
    public ConfigService(EnvService env)
    {
        ArgumentNullException.ThrowIfNull(env);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        _map = new MapCfgCache();
        _item = new ItemCfgCache();
        _basic = new BasicCfgCache();
        _equipment = new EquipmentCfgCache();
        _caches = [_map, _item, _basic, _equipment];
        try { Load(env.ConfigDirectory, env.SchemaDirectory); }
        catch { Dispose(); throw; }
    }

    /// <summary>按标注文件名匹配全部 JSON，再由缓存自身决定单例或多例加载。</summary>
    private void Load(string configDirectory, string schemaDirectory)
    {
        var caches = _caches.ToDictionary(cache => cache.FileName, StringComparer.Ordinal);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        Scan(configDirectory, files, caches);
        foreach (var cache in _caches)
        {
            if (!files.ContainsKey(cache.FileName))
                throw new InvalidDataException($"缺少配置文件：{configDirectory}/{cache.FileName}");
        }
        foreach (var cache in _caches) cache.Load(files[cache.FileName], schemaDirectory);
        foreach (var cache in _caches) cache.ValidateReferences(this);
    }

    /// <summary>通过 Godot 收集配置根目录的文件，拒绝子目录和未注册文件名。</summary>
    private static void Scan(string directory, Dictionary<string, string> files, IReadOnlyDictionary<string, CfgCache> caches)
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
            if (dir.CurrentIsDir()) throw new InvalidDataException($"配置目录不允许子目录：{path}");
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!caches.ContainsKey(name))
                    throw new InvalidDataException($"未注册的配置文件：{path}；允许文件名：{string.Join(", ", caches.Keys)}");
                if (!files.TryAdd(name, path))
                    throw new InvalidDataException($"重复的配置文件：{files[name]} 与 {path}");
            }
        }
        dir.ListDirEnd();
    }

    /// <summary>回收全部配置缓存，已取得的缓存引用同步失效。</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var cache in _caches) cache.Dispose();
    }
}
