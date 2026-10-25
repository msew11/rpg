using System;
using Rpg.Session.Data;

namespace Rpg.World.Components;

/// <summary>地图动态对象的服务器身份及互斥载荷。</summary>
internal sealed class DataMapObjectComponent
{
    /// <summary>服务器分配的稳定对象身份。</summary>
    public Guid ObjectId { get; }
    /// <summary>当前权威版本。</summary>
    public long Revision { get; set; }
    /// <summary>怪物类型，地面物品时为空。</summary>
    public string? MonsterTypeId { get; set; }
    /// <summary>地面物品编号，怪物时为零。</summary>
    public int ItemId { get; set; }
    /// <summary>地面物品数量，怪物时为零。</summary>
    public int Count { get; set; }

    /// <summary>使用已验证的会话对象建立组件。</summary>
    public DataMapObjectComponent(DataMapObjectPbData value)
    {
        ObjectId = value.ObjectId;
        Revision = value.Revision;
        MonsterTypeId = value.MonsterTypeId;
        ItemId = value.ItemId;
        Count = value.Count;
    }
}
