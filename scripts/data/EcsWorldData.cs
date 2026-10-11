using System;
using System.Collections.Generic;

/// <summary>承载一个 ECS 世界的实体标识与组件状态。</summary>
public sealed class EcsWorldData : IEcsWorldData, IDataRepoData
{
    /// <summary>分配给下一个实体的标识，销毁后不复用。</summary>
    public long NextEntityId { get; set; } = 1;

    /// <summary>记录当前有效的实体标识。</summary>
    public ISet<long> Entities { get; } = new HashSet<long>();

    /// <summary>按实体标识和组件精确类型保存组件。</summary>
    public IDictionary<long, Dictionary<Type, EcsComponent>> Components { get; } =
        new Dictionary<long, Dictionary<Type, EcsComponent>>();
}
