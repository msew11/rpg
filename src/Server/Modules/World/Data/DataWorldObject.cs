using System;

namespace Rpg.Server.Modules.World.Data;

/// <summary>动态对象的不可变数据，不附带业务校验或协议转换。</summary>
public sealed record DataWorldObject
{
    /// <summary>跨块移动仍保持的对象身份。</summary>
    public Guid ObjectId { get; init; }
    /// <summary>对象自身版本，与存储提交编号独立。</summary>
    public long Revision { get; init; }
    /// <summary>未投影的逻辑横坐标。</summary>
    public float X { get; init; }
    /// <summary>未投影的逻辑纵坐标。</summary>
    public float Y { get; init; }
    /// <summary>怪物类型，存储层不验证载荷组合。</summary>
    public string? MonsterTypeId { get; init; }
    /// <summary>道具编号，存储层不检查配置引用。</summary>
    public int ItemId { get; init; }
    /// <summary>对象承载数量，存储层不限制数值。</summary>
    public int Count { get; init; }
}
