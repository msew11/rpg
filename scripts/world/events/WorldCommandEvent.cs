/// <summary>承载世界交互结果，供管理器发出对应视图通知。</summary>
public abstract partial class WorldCommandEvent : EcsEvent
{
    /// <summary>事件是否改变了区块或区块子实体。</summary>
    public bool chunks_changed { get; set; }

    /// <summary>事件是否改变了调试显示状态。</summary>
    public bool debug_changed { get; set; }
}
