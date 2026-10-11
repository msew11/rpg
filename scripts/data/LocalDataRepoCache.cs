using System;
using Godot;

/// <summary>管理一个本地会话中的数据仓库。</summary>
[GlobalClass]
public partial class LocalDataRepoCache : DataRepoCache
{
    /// <summary>当前会话的 ECS 世界仓库，绑定前为空。</summary>
    private RepoWorldEcs? _worldRepo;

    /// <summary>将一个世界的数据绑定到会话缓存；重复绑定会失败。</summary>
    public void BindWorld(DataEcsWorld data)
    {
        if (_worldRepo is not null)
            throw new InvalidOperationException("The session cache already has an ECS world.");
        _worldRepo = new RepoWorldEcs(data);
    }

    /// <summary>按仓库的精确 C# 类型读取已注册仓库。</summary>
    public override IDataRepo? GetRepo(Type repoType) =>
        repoType == typeof(RepoWorldEcs) ? _worldRepo : null;
}
