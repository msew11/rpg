using System;

/// <summary>约束具体仓库承载的数据类型，不规定读写或持久化方式。</summary>
public abstract class DataRepo<T> : IDataRepo where T : IDataRepoData
{
    /// <summary>返回编译期确定的数据类型。</summary>
    public Type GetDataType() => typeof(T);

    /// <summary>由具体仓库声明实例数量形态。</summary>
    public abstract DataRepoStorageKind GetStorageKind();
}
