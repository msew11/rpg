using Godot;

namespace Rpg.World.Actions;

/// <summary>更新角色移动目的地的输入消息。</summary>
public sealed record MoveAction(long ActionId, Vector2 Destination) : WorldAction;
