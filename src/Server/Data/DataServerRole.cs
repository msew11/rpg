namespace Rpg.Server.Data;

/// <summary>服务端自己的性别定义，不依赖客户端类型。</summary>
public enum ServerRoleGender
{
    /// <summary>男性基础身体。</summary>
    Male,
    /// <summary>女性基础身体。</summary>
    Female
}

/// <summary>服务端使用的不可变角色业务候选。</summary>
public sealed record DataServerRole
{
    /// <summary>角色唯一标识。</summary>
    public long RoleId { get; init; }
    /// <summary>角色所属地图，内存候选默认使用地图 1。</summary>
    public int MapId { get; init; } = 1;
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
    /// <summary>头型编号，内存候选默认使用头型 1。</summary>
    public int HeadId { get; init; } = 1;
    /// <summary>持久化删除标记，保留编号防止重启恢复。</summary>
    public bool IsDeleted { get; init; }
    /// <summary>仅供业务候选比较的进程内状态序号。</summary>
    public long Revision { get; init; }
    /// <summary>本次会话已经确认的移动序号。</summary>
    public long AcknowledgedAction { get; init; }
    /// <summary>角色额外解锁的背包行数。</summary>
    public int BagExtraRows { get; init; }
    /// <summary>背包与穿戴的业务候选视图，由实体缓存拆表。</summary>
    public DataServerBag Bag { get; init; } = new();
}
