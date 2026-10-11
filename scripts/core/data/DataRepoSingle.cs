/// <summary>为只承载一个数据实例的仓库固定单例形态。</summary>
public abstract class DataRepoSingle<T> : DataRepo<T> where T : IDataRepoData
{
    /// <summary>返回仓库承载的唯一数据实例。</summary>
    public abstract T Data { get; }

    /// <summary>返回不可由具体仓库改写的单例形态。</summary>
    public sealed override DataRepoStorageKind GetStorageKind() => DataRepoStorageKind.Single;
}
