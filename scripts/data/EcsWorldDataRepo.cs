/// <summary>承载本地会话内唯一的 ECS 世界数据。</summary>
public sealed class EcsWorldDataRepo : DataRepoSingle<EcsWorldData>
{
    /// <summary>仓库创建并承载的世界数据实例。</summary>
    public override EcsWorldData Data { get; } = new();
}
