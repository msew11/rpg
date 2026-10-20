namespace Rpg.World.Systems;

/// <summary>轻量 ECS 系统的固定帧更新契约。</summary>
internal interface IWorldSystem
{
    /// <summary>对已创建角色实体执行一个固定帧。</summary>
    void Update(EntityStore entities, int roleEntity, double delta);
}
