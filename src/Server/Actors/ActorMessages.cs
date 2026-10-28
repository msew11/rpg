using System;
using System.Collections.Generic;
using Google.Protobuf;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;

namespace Rpg.Server.Actors;

/// <summary>无业务载荷的初始化探针。</summary>
internal sealed record DataInitialize;
/// <summary>角色分片消息始终携带所需身份。</summary>
internal sealed record DataRoleRequest(long RoleId, IMessage Request);
/// <summary>指定角色回合中的内部操作，不跨回合保留上下文。</summary>
internal sealed record DataRoleWork(long RoleId, Func<ServerRequestContext, object?> Work, bool InWorld = false);
/// <summary>地图事务携带独占的角色工作副本，回合结束即移交结果。</summary>
internal sealed record DataWorldWork(int MapId, ServerRoleCache Role, Func<ServerRequestContext, object?> Work);
/// <summary>指定地图上的独立操作。</summary>
internal sealed record DataMapWork(int MapId, Func<ServerWorldCache, object?> Work);
/// <summary>角色回合结果，不保留到发送端之外。</summary>
internal sealed record DataRoleResult(object? Response, ServerRoleCache Role, IMessage[] Notices);
/// <summary>登录请求与当前会话身份。</summary>
internal sealed record DataLoginRequest(IMessage Request, bool Authenticated, bool Selected);
/// <summary>跨 Actor 发布不可变摘要。</summary>
internal sealed record DataSummaryChanged(RoleSummaryEntity Summary);
/// <summary>角色初始化前领取未落地的新建数据。</summary>
internal sealed record DataClaimRole(long RoleId);
/// <summary>角色初始化数据为空表示从独立三表加载。</summary>
internal sealed record DataRoleSeed(RoleEntityTables? Tables);
/// <summary>读取或提交登录暂存结果的有序回合。</summary>
internal sealed record DataLoginWork(Func<ServerRequestContext, object?> Work);
/// <summary>地图的独立只读快照，不泄露可写缓存。</summary>
internal sealed record DataServerMap(Map.MapLayout Rules, IReadOnlyDictionary<Guid, MapObjectEntity> Objects);
