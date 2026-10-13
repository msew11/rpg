using System;
using Godot;

/// <summary>管理一个本地会话中的数据仓库。</summary>
[GlobalClass]
public partial class LocalDataRepoCache : DataRepoCache
{
    /// <summary>创建缓存时即存在的 ECS 世界单例仓库。</summary>
    internal EcsWorldDataRepo WorldRepo { get; } = new();

    /// <summary>当前会话的临时存档入口。</summary>
    private GodotObject? _workingStore;

    /// <summary>绑定仅供本会话区块加载和落地使用的临时存档。</summary>
    public void bind_working_store(GodotObject store) => _workingStore = store;

    /// <summary>从临时存档读取区块变化；无记录时使用配置基准。</summary>
    public byte[] load_chunk(Vector2I canonicalId, byte[] baseline) =>
        _workingStore?.Call("load_chunk", canonicalId, baseline).AsByteArray() ?? (byte[])baseline.Clone();

    /// <summary>尝试把卸载区块的变化写入临时存档。</summary>
    public bool unload_chunk(Vector2I canonicalId, byte[] tiles, byte[] baseline, string entitiesJson) =>
        _workingStore?.Call("unload_chunk", canonicalId, tiles, baseline, entitiesJson).AsBool() ?? false;

    /// <summary>读取已落地区块拥有的子实体记录。</summary>
    public string load_entities_json(Vector2I canonicalId) =>
        _workingStore?.Call("load_entities_json", canonicalId).AsString() ?? "{}";

    /// <summary>按仓库的精确 C# 类型读取已注册仓库。</summary>
    public override IDataRepo? GetRepo(Type repoType) =>
        repoType == typeof(EcsWorldDataRepo) ? WorldRepo : null;
}
