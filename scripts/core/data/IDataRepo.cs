using System;

/// <summary>为统一管理器提供不依赖泛型参数的仓库契约。</summary>
public interface IDataRepo
{
    /// <summary>返回仓库承载的数据类型。</summary>
    Type GetDataType();

    /// <summary>返回仓库承载单个还是多个数据实例。</summary>
    DataRepoStorageKind GetStorageKind();
}
