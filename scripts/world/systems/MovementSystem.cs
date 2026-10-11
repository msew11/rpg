using Godot;

/// <summary>根据移动意图在 ECS 固定步中更新位置与朝向。</summary>
public partial class MovementSystem : EcsSystem
{
    /// <summary>移动有输入的实体，并保留静止实体的朝向。</summary>
    public override void Update(EcsWorld world, double delta)
    {
        foreach (var entityId in world.Query(typeof(PositionComponent), typeof(MovementIntentComponent),
                     typeof(MoveSpeedComponent), typeof(FacingComponent)))
        {
            var intent = world.GetComponent<MovementIntentComponent>(entityId)!;
            if (intent.value == Vector2I.Zero)
                continue;
            var position = world.GetComponent<PositionComponent>(entityId)!;
            var speed = world.GetComponent<MoveSpeedComponent>(entityId)!;
            var facing = world.GetComponent<FacingComponent>(entityId)!;
            position.value += WorldMath.MovementDirection(intent.value) * (float)(speed.value * delta);
            facing.value = intent.value;
        }
    }
}
