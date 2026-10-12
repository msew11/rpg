/// <summary>处理世界调试显示开关事件。</summary>
public partial class DebugSettingsSystem : EcsSystem
{
    /// <summary>此系统没有逐帧行为。</summary>
    public override void Update(EcsWorld world, double delta) { }

    /// <summary>更新区块边缘线状态。</summary>
    [EcsEventHandler(typeof(SetDebugEdgesEvent))]
    private void OnSetEdges(EcsWorld world, SetDebugEdgesEvent gameEvent)
    {
        var ids = world.Query(typeof(WorldDebugComponent));
        if (ids.Length == 0)
            return;
        var debug = world.GetComponent<WorldDebugComponent>(ids[0])!;
        if (debug.edges == gameEvent.enabled)
            return;
        debug.edges = gameEvent.enabled;
    }

    /// <summary>更新雷达状态。</summary>
    [EcsEventHandler(typeof(SetDebugRadarEvent))]
    private void OnSetRadar(EcsWorld world, SetDebugRadarEvent gameEvent)
    {
        var ids = world.Query(typeof(WorldDebugComponent));
        if (ids.Length == 0)
            return;
        var debug = world.GetComponent<WorldDebugComponent>(ids[0])!;
        if (debug.radar == gameEvent.enabled)
            return;
        debug.radar = gameEvent.enabled;
    }
}
