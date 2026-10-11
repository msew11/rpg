/// <summary>承载本地会话内唯一的 ECS 世界数据。</summary>
public sealed class DataRepoEcsWorld : DataRepoSingle<DataEcsWorld>
{
    /// <summary>仓库创建并承载的世界数据实例。</summary>
    public override DataEcsWorld Data { get; } = new();
}
