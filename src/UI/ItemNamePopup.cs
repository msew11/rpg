using Godot;

namespace Rpg.UI;

/// <summary>背包、装备与地面共用的即时道具信息浮层，目前显示名称并避开鼠标和视口边缘。</summary>
public partial class ItemNamePopup : Label
{
    /// <summary>建立统一名称样式，不参与鼠标命中。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Hide();
        AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color(0.08f, 0.10f, 0.14f, 0.95f),
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 });
    }

    /// <summary>按配置名称立即显示，名称变化时重新计算自然尺寸。</summary>
    public void ShowName(string name, Vector2 pointer)
    {
        Text = name;
        Size = GetCombinedMinimumSize();
        Show();
        MoveTo(pointer);
    }

    /// <summary>名称跟随鼠标，并保持在视口内部。</summary>
    public void MoveTo(Vector2 pointer) => Position = (pointer + new Vector2(24, 24)).Min(
        (GetViewportRect().Size - Size).Max(Vector2.Zero)).Max(Vector2.Zero);
}
