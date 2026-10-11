using System;
using Godot;

/// <summary>验证 openspec/specs/player-movement 与 world-script-boundaries 的 ECS 世界状态契约。</summary>
public partial class TestWorldEcs : SceneTree
{
    /// <summary>检查世界实体和玩家组件确实由共享 ECS 保存并更新。</summary>
    public override void _Initialize()
    {
        try
        {
            using var state = new WorldState();
            var ecs = state.ecs;
            var worldId = state.world_entity_id;
            Check(ecs.HasEntity(worldId), "world entity exists");
            Check(ReferenceEquals(ecs.GetComponent<WorldChunksComponent>(worldId), state.chunks()), "world chunks use ECS");
            Check(ReferenceEquals(ecs.GetComponent<WorldDebugComponent>(worldId), state.debug()), "world debug uses ECS");

            var playerId = state.create_player(Vector2.Zero, 3);
            Check(ecs.HasEntity(playerId), "player entity exists");
            Check(ecs.GetComponent<PositionComponent>(playerId)?.value == Vector2.Zero, "initial position uses ECS");
            Check(ecs.GetComponent<MovementIntentComponent>(playerId)?.value == Vector2I.Zero, "initial intent uses ECS");
            Check(ecs.GetComponent<MoveSpeedComponent>(playerId)?.value == 3, "movement speed uses ECS");
            Check(ecs.GetComponent<FacingComponent>(playerId)?.value == new Vector2I(0, 1), "initial facing uses ECS");
            Check(ecs.HasComponent<LocalPlayerComponent>(playerId), "local player marker uses ECS");

            state.set_player_intent(playerId, new Vector2I(1, 0));
            state.step(1);
            Check(state.player_position(playerId) == ecs.GetComponent<PositionComponent>(playerId)?.value, "world reads ECS position");
            Check(state.player_facing(playerId) == ecs.GetComponent<FacingComponent>(playerId)?.value, "world reads ECS facing");
            GD.Print("test_world_ecs passed");
            Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr(exception);
            Quit(1);
        }
    }

    /// <summary>报告不满足的 ECS 状态条件。</summary>
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
    }
}
