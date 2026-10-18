using Godot;
using Rpg.Session;

namespace Rpg.World;

/// <summary>所有外部变化进入 ECS 的统一消息基类。</summary>
public abstract record WorldAction;

/// <summary>初始化地图世界实体的消息。</summary>
public sealed record InitializeWorldAction(MapManager Map) : WorldAction;

/// <summary>初始化选中角色实体的消息。</summary>
public sealed record InitializeRoleAction(DataRole Role) : WorldAction;

/// <summary>更新角色移动目的地的输入消息。</summary>
public sealed record MoveAction(long ActionId, Vector2 Destination) : WorldAction;

/// <summary>由会话缓存转来的权威角色状态消息。</summary>
public sealed record SyncRoleAction(DataRoleState State) : WorldAction;
