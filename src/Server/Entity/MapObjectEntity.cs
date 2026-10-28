using System;
using System.IO;
using System.Text.Json.Serialization;
using Rpg.Net.Message;

namespace Rpg.Server.Entity;

/// <summary>地图动态对象的不可变状态；两种载荷仅允许出现一种。</summary>
public sealed record MapObjectEntity
{
    /// <summary>服务器分配的稳定 GUID，不随跨块移动变化。</summary>
    [JsonRequired]
    public Guid ObjectId { get; init; } = Guid.NewGuid();
    /// <summary>本对象的递增版本，与落地批次及网络序号无关。</summary>
    [JsonRequired]
    public long Revision { get; init; } = 1;
    /// <summary>未投影逻辑横坐标。</summary>
    [JsonRequired]
    public float X { get; init; }
    /// <summary>未投影逻辑纵坐标。</summary>
    [JsonRequired]
    public float Y { get; init; }
    /// <summary>怪物类型标识，空值表示地面道具。</summary>
    public string? MonsterTypeId { get; init; }
    /// <summary>地面道具的配置编号，怪物时为零。</summary>
    public int ItemId { get; init; }
    /// <summary>地面道具数量，怪物时为零。</summary>
    public int Count { get; init; }

    /// <summary>验证身份、版本和载荷；道具配置契约由所属地图进一步检查。</summary>
    public void Validate()
    {
        if (ObjectId == Guid.Empty || Revision <= 0 || !float.IsFinite(X) || !float.IsFinite(Y) ||
            (MonsterTypeId is null ? ItemId <= 0 || Count <= 0 :
                string.IsNullOrWhiteSpace(MonsterTypeId) || ItemId != 0 || Count != 0))
            throw new InvalidDataException("地图对象身份或载荷无效");
    }

    /// <summary>复制独立协议载荷，不共享服务端状态。</summary>
    public MapObjectPb ToPb()
    {
        var message = new MapObjectPb { ObjectId = ObjectId.ToString("N"), Revision = Revision, X = X, Y = Y };
        if (MonsterTypeId is not null) message.Monster = new MapMonsterPb { MonsterTypeId = MonsterTypeId };
        else message.GroundItem = new MapGroundItemPb { ItemId = ItemId, Count = Count };
        return message;
    }
}
