namespace Rpg.World.Actions;

/// <summary>初始化地图世界实体的消息。</summary>
public sealed record InitializeWorldAction(MapManager Map) : WorldAction;
