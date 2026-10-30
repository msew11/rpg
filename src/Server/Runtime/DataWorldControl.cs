namespace Rpg.Server.Runtime;

/// <summary>角色运行时控制屏障，不属于业务 protobuf。</summary>
internal sealed record DataRoleControl(long ServerTimeMs, int Speed) : IActorRequest<bool>;

/// <summary>地图在线身份控制，入队后只在所属邮箱操作缓存。</summary>
internal sealed record DataWorldMembership(long RoleId, bool Online) : IActorRequest<bool>;
