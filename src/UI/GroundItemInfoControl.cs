using System;
using Godot;

namespace Rpg.UI;

/// <summary>在 UI 画布上显示地面物品信息并接受拾取点击。</summary>
public partial class GroundItemInfoControl : PanelContainer
{
    /// <summary>对应的地面对象身份。</summary>
    public Guid ObjectId { get; }
    /// <summary>当前悬停高亮状态。</summary>
    public bool IsHighlighted { get; private set; }
    /// <summary>请求拾取对应地面对象。</summary>
    public event Action<Guid>? PickupRequested;

    /// <summary>信息文本，单件不附加数量。</summary>
    private readonly Label _label = new() { Name = "InfoText", MouseFilter = MouseFilterEnum.Ignore };
    /// <summary>普通信息框样式。</summary>
    private readonly StyleBoxFlat _normal = new()
    {
        BgColor = new Color(0.08f, 0.10f, 0.14f, 0.94f),
        BorderColor = Colors.Transparent,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 2, ContentMarginBottom = 2
    };
    /// <summary>鼠标悬停时的高亮信息框样式。</summary>
    private readonly StyleBoxFlat _highlight = new()
    {
        BgColor = new Color(0.18f, 0.25f, 0.29f, 0.98f),
        BorderColor = new Color(0.91f, 0.78f, 0.32f),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 2, ContentMarginBottom = 2
    };

    /// <summary>创建可接收鼠标但不改变世界对象的 UI 控件。</summary>
    public GroundItemInfoControl(Guid objectId)
    {
        ObjectId = objectId;
        Name = "GroundItemInfo_" + objectId.ToString("N");
        MouseFilter = MouseFilterEnum.Stop;
        _label.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_label);
        AddThemeStyleboxOverride("panel", _normal);
    }

    /// <summary>同步当前物品名称和可堆叠数量。</summary>
    public void SetItem(string name, int count)
    {
        _label.Text = count > 1 ? $"{name} ×{count}" : name;
        ResetSize();
    }

    /// <summary>切换鼠标反馈样式。</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (IsHighlighted == highlighted) return;
        IsHighlighted = highlighted;
        AddThemeStyleboxOverride("panel", highlighted ? _highlight : _normal);
    }

    /// <summary>左键点击由 UI 消费并转为现有拾取请求。</summary>
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            if (!Rpg.Services.GameServices.Session.IsWorldReadOnly) PickupRequested?.Invoke(ObjectId);
            AcceptEvent();
        }
    }
}
