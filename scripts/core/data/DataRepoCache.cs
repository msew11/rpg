using System;
using Godot;

/// <summary>定义不依赖场景树的统一仓库管理器类型查找入口。</summary>
public abstract partial class DataRepoCache : RefCounted
{
    /// <summary>按仓库类型取得实例，未注册时返回空值。</summary>
    public abstract IDataRepo? GetRepo(Type repoType);
}
