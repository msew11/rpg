namespace Rpg.World.Systems;

/// <summary>轻量 ECS 系统的固定帧更新契约。</summary>
internal interface IWorldSystem
{
    /// <summary>从当前世界读取实体数据并推进一个固定帧。</summary>
    void Update(WorldManager world, double delta);
}
