using System.Text.Json.Serialization;

namespace Rpg.Server.Entity;

/// <summary>角色单例表，不包含物品列表或网络消息版本。</summary>
public sealed record RoleEntity
{
    /// <summary>角色唯一标识。</summary>
    public long RoleId { get; init; }
    /// <summary>角色所属地图，必须为正数。</summary>
    [JsonRequired]
    public int MapId { get; init; }
    /// <summary>显示名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>角色等级。</summary>
    public int Level { get; init; }
    /// <summary>当前生命。</summary>
    public int Health { get; init; }
    /// <summary>生命上限。</summary>
    public int MaxHealth { get; init; }
    /// <summary>当前魔力。</summary>
    public int Mana { get; init; }
    /// <summary>魔力上限。</summary>
    public int MaxMana { get; init; }
    /// <summary>当前经验。</summary>
    public int Experience { get; init; }
    /// <summary>下次升级所需经验。</summary>
    public int NextExperience { get; init; }
    /// <summary>权威逻辑横坐标。</summary>
    public float X { get; init; }
    /// <summary>权威逻辑纵坐标。</summary>
    public float Y { get; init; }
    /// <summary>基础身体性别。</summary>
    public ServerRoleGender Gender { get; init; }
    /// <summary>必须保存的合法头型编号。</summary>
    [JsonRequired]
    public int HeadId { get; init; }
    /// <summary>持久化删除标记，保留编号防止重启恢复。</summary>
    public bool IsDeleted { get; init; }
    /// <summary>角色额外解锁的背包行数。</summary>
    public int BagExtraRows { get; init; }
    /// <summary>下次创建物品的稳定编号，已删除编号不复用。</summary>
    public long NextInstanceId { get; init; } = 1;
}
