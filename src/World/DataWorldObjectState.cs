using System;
using System.Collections.Generic;

namespace Rpg.World;

/// <summary>从 ECS 组件生成的只读地图对象表现状态。</summary>
public sealed record DataWorldObjectState(int EntityId, Guid ObjectId, long Revision, float X, float Y,
    string? MonsterTypeId, int ItemId, int Count);

/// <summary>一次完整 AOI 同步后的只读 ECS 对象集合。</summary>
public sealed record DataWorldObjectsState(long Sequence, IReadOnlyDictionary<Guid, DataWorldObjectState> Objects);
