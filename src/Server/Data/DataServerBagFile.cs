using System.Text.Json.Serialization;

namespace Rpg.Server.Data;

/// <summary>独立背包领域存档，显式记录角色归属与扩容状态。</summary>
public sealed record DataServerBagFile
{
    /// <summary>此背包所属角色，用于拒绝文件错配。</summary>
    [JsonRequired]
    public long RoleId { get; init; }
    /// <summary>角色已解锁的额外背包行数。</summary>
    [JsonRequired]
    public int BagExtraRows { get; init; }
    /// <summary>物品实例、背包版本和下次实例编号。</summary>
    [JsonRequired]
    public DataServerBag Bag { get; init; } = new();

    /// <summary>从服务器内存聚合复制背包领域，不引入客户端依赖。</summary>
    public static DataServerBagFile FromRole(DataServerRole role) => new()
    {
        RoleId = role.RoleId, BagExtraRows = role.BagExtraRows, Bag = role.Bag
    };
}
