/// <summary>为承载多个同类型数据实例的仓库固定多例形态。</summary>
public abstract class DataRepoMultiple<T> : DataRepo<T> where T : IDataRepoData
{
    /// <summary>返回不可由具体仓库改写的多例形态。</summary>
    public sealed override DataRepoStorageKind GetStorageKind() => DataRepoStorageKind.Multiple;
}
