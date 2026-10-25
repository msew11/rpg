using System.Collections.Generic;

namespace Rpg.Settings.Data;

/// <summary>单个 HUD 元素的相对屏幕左上坐标。</summary>
public sealed class HudPosition
{
    /// <summary>相对横向坐标。</summary>
    public float X { get; set; }
    /// <summary>相对纵向坐标。</summary>
    public float Y { get; set; }

    /// <summary>创建一个可保存的相对位置。</summary>
    public HudPosition() { }

    /// <summary>按给定坐标创建默认相对位置。</summary>
    public HudPosition(float x, float y) { X = x; Y = y; }
}

/// <summary>按角色独立持久化的技能栏显隐与 HUD 布局。</summary>
public sealed class RoleSetting
{
    /// <summary>三条技能栏各自是否可见。</summary>
    public bool[] VisibleHotbars { get; set; } = [true, false, false];
    /// <summary>头像、技能栏、系统按钮条、小地图和聊天框的相对位置。</summary>
    public Dictionary<string, HudPosition> HudPositions { get; set; } = DefaultPositions();

    /// <summary>为未保存过设置的角色提供默认 HUD 布局。</summary>
    public static Dictionary<string, HudPosition> DefaultPositions() => new()
    {
        ["portrait"] = new(0.015f, 0.02f),
        ["hotbar1"] = new(0.31f, 0.88f),
        ["hotbar2"] = new(0.31f, 0.77f),
        ["hotbar3"] = new(0.31f, 0.66f),
        ["menu"] = new(0.76f, 0.90f),
        ["radar"] = new(0.85f, 0.02f),
        ["chat"] = new(0.015f, 0.72f)
    };
}
