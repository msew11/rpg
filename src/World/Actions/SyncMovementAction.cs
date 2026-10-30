using Godot;

namespace Rpg.World.Actions;

/// <summary>统一应用本人权威纠偏和仍未确认的最新移动目标。</summary>
internal sealed record SyncMovementAction(Vector2 Position, Vector2? Target) : WorldAction;

/// <summary>提交经插值的远端状态；空集合清除远端实体。</summary>
internal sealed record SyncRemoteActorsAction(System.Collections.Generic.IReadOnlyList<DataActorState> Actors) : WorldAction;
