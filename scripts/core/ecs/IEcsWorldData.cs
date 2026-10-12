using System;
using System.Collections.Generic;

/// <summary>提供 ECS 世界的实体标识及组件状态读写入口。</summary>
public interface IEcsWorldData
{
    /// <summary>下一个待分配的实体标识。</summary>
    long NextEntityId { get; set; }

    /// <summary>当前有效的实体标识集合。</summary>
    ISet<long> Entities { get; }

    /// <summary>按实体标识和组件精确类型保存组件。</summary>
    IDictionary<long, Dictionary<Type, EcsComponent>> Components { get; }

    /// <summary>记录每个子实体的父实体标识。</summary>
    IDictionary<long, long> ParentByEntity { get; }

    /// <summary>记录每个实体的直接子实体集合。</summary>
    IDictionary<long, HashSet<long>> ChildrenByEntity { get; }
}
