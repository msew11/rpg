using System;
using Godot;

/// <summary>定义未来统一仓库管理器的类型查找入口。</summary>
public abstract partial class DataRepoCache : Node
{
    /// <summary>按仓库类型取得实例，未注册时返回空值。</summary>
    public abstract IDataRepo? GetRepo(Type repoType);
}
