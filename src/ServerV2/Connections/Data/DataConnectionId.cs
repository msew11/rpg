using System;

namespace Rpg.ServerV2.Connections.Data;

/// <summary>服务端分配的连接身份，不进入客户端协议，也不在重连时复用。</summary>
/// <param name="Value">该连接的唯一标识。</param>
public readonly record struct DataConnectionId(Guid Value);

/// <summary>某一代次的不可变会话快照，不向调用方暴露可写会话。</summary>
/// <param name="ConnectionId">传输端绑定的连接身份。</param>
/// <param name="Generation">当前会话代次。</param>
/// <param name="Username">登录成功后绑定的规范账号键。</param>
/// <param name="RoleId">成功进入后绑定的角色，零表示尚未进入。</param>
public sealed record DataSessionSnapshot(DataConnectionId ConnectionId, ulong Generation, string? Username, long RoleId = 0, long AuthEpoch = 0);
