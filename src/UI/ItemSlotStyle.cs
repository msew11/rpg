using Godot;

namespace Rpg.UI;

/// <summary>背包与装备共用的格子外框、内容区域及图标显示规则。</summary>
public static class ItemSlotStyle
{
    /// <summary>含一像素边框的正方形格子边长。</summary>
    public const float SlotSize = 48;
    /// <summary>边框内部的物品图标区域边长。</summary>
    public const float ContentSize = 46;
    /// <summary>网格及装备各行之间的统一间隔。</summary>
    public const int Spacing = 4;

    /// <summary>设置格子相同的尺寸、底色与边框。</summary>
    public static void Apply(PanelContainer panel)
    {
        panel.CustomMinimumSize = Vector2.One * SlotSize;
        var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.13f, 0.17f), BorderColor = new Color(0.35f, 0.40f, 0.48f) };
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
    }

    /// <summary>创建不参与鼠标命中的统一内容区域，数量标签由背包自行添加。</summary>
    public static Control CreateContent() => new()
    {
        Name = "ItemContent", CustomMinimumSize = Vector2.One * ContentSize, MouseFilter = Control.MouseFilterEnum.Ignore
    };

    /// <summary>统一格内与拿起图像，保留背包的图标区域、缺图占位和可选数量。</summary>
    public static Control CreateContent(Texture2D? texture, int? quantity)
    {
        var content = CreateContent();
        Control icon = texture is null
            ? new Label { Name = "MissingIcon", Text = "?", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore }
            : CreateIcon("ItemIcon");
        if (icon is TextureRect image) image.Texture = texture;
        content.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (quantity is { } count)
        {
            var label = new Label { Name = "ItemCount", Text = count.ToString(), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 4);
            content.AddChild(label);
            label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        return content;
    }

    /// <summary>图标在完整内容区域内等比居中，所有槽位采用同一采样方式。</summary>
    public static TextureRect CreateIcon(string name) => new()
    {
        Name = name, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };
}
