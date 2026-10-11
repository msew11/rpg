/// <summary>承载本地会话内唯一的 ECS 世界数据。</summary>
public sealed class RepoWorldEcs : DataRepoSingle<DataEcsWorld>
{
    /// <summary>仓库当前承载的世界数据实例。</summary>
    public DataEcsWorld Data { get; }

    /// <summary>接管指定 ECS 世界数据实例。</summary>
    public RepoWorldEcs(DataEcsWorld data) => Data = data;
}
