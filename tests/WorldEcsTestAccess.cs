using System;
using Godot;

/// <summary>为 openspec/specs/world-script-boundaries 的边界验收提供 ECS 观察与样本布置入口。</summary>
[GlobalClass]
public partial class WorldEcsTestAccess : RefCounted
{
    /// <summary>从会话缓存取得测试用 ECS 视图，不注册或调度世界 System。</summary>
    public static EcsWorld FromCache(LocalDataRepoCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var repo = cache.GetRepo(typeof(EcsWorldDataRepo)) as EcsWorldDataRepo;
        return new EcsWorld(repo?.Data ?? throw new InvalidOperationException("Missing ECS world repository."));
    }

    /// <summary>为实体关系测试创建玩家样本。</summary>
    public static long CreatePlayer(WorldManager world, Vector2 position, float speed) =>
        world.create_player(position, speed);

    /// <summary>为区块和存档测试布置玩家位置并保留变化通知。</summary>
    public static void SetPlayerPosition(WorldManager world, long entityId, Vector2 value) =>
        world.set_player_position(entityId, value);

    /// <summary>为视图和存档测试布置玩家朝向并保留变化通知。</summary>
    public static void SetPlayerFacing(WorldManager world, long entityId, Vector2I value) =>
        world.set_player_facing(entityId, value);

    /// <summary>为地块副本测试布置规范区块内容。</summary>
    public static bool SetChunkTiles(WorldManager world, Vector2I canonicalId, byte[] tiles) =>
        world.set_chunk_tiles(canonicalId, tiles);

    /// <summary>为区块窗口测试绑定配置并加载附近区块。</summary>
    public static void InitializeTestChunks(WorldManager world, GodotObject cfg) =>
        world.initialize_test_chunks(cfg);

    /// <summary>确认已启动会话拒绝再次初始化世界。</summary>
    public static bool ReinitializeRejected(WorldManager world, GodotObject cfg)
    {
        try
        {
            world.initialize_session(Vector2.Zero, 3, new Vector2I(0, 1), cfg);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
