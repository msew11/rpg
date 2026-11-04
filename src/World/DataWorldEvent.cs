using System;
using Rpg.World.Actions;

namespace Rpg.World;

/// <summary>由世界输出、协调层按顺序取走的网络意图。</summary>
internal abstract record DataWorldEvent;

/// <summary>复用移动合并与确认机制的走近或停止请求。</summary>
internal sealed record DataMoveRequested(MoveAction Action, bool Force) : DataWorldEvent;

/// <summary>对应已建立预测的单次起手请求。</summary>
internal sealed record DataAttackRequested(ulong AttackId, Guid Target) : DataWorldEvent;

/// <summary>须在随后移动之前发送的攻击取消。</summary>
internal sealed record DataAttackCancelRequested(ulong ActionId) : DataWorldEvent;
