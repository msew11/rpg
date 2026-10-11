using System;
using Godot;

/// <summary>管理一个本地会话中的数据仓库。</summary>
[GlobalClass]
public partial class LocalDataRepoCache : DataRepoCache
{
    /// <summary>创建缓存时即存在的 ECS 世界单例仓库。</summary>
    internal DataRepoEcsWorld WorldRepo { get; } = new();

    /// <summary>使用本缓存的数据仓库创建世界操作入口。</summary>
    public WorldState CreateWorldState() => new(this);

    /// <summary>按仓库的精确 C# 类型读取已注册仓库。</summary>
    public override IDataRepo? GetRepo(Type repoType) =>
        repoType == typeof(DataRepoEcsWorld) ? WorldRepo : null;
}
