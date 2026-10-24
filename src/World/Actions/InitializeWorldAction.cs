using Rpg.World.Components;

namespace Rpg.World.Actions;

/// <summary>初始化地图世界实体的消息。</summary>
internal sealed record InitializeWorldAction(DataMapComponent Map) : WorldAction;
