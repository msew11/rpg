namespace Rpg.Settings.Data;

/// <summary>按逻辑像素保存的聊天框手动尺寸。</summary>
public sealed class DataHudSize
{
    /// <summary>默认聊天宽度，兼容未保存尺寸的角色。</summary>
    public const float DefaultWidth = 320;
    /// <summary>默认聊天高度。</summary>
    public const float DefaultHeight = 180;
    /// <summary>保留输入区可用空间的最小宽度。</summary>
    public const float MinimumWidth = 160;
    /// <summary>保留消息与输入区的最小高度。</summary>
    public const float MinimumHeight = 100;
    /// <summary>用户选择的逻辑像素宽度。</summary>
    public float Width { get; set; } = DefaultWidth;
    /// <summary>用户选择的逻辑像素高度。</summary>
    public float Height { get; set; } = DefaultHeight;
}
