using Godot;

namespace Rpg.UI;

/// <summary>背包格子的点选入口，立即显示名称并由面板托管拿起状态。</summary>
public partial class BagSlotControl : PanelContainer
{
    /// <summary>负责校验拖放上下文并提交请求的背包面板。</summary>
    internal BagControl Bag { get; init; } = null!;
    /// <summary>此格子在背包中的固定编号。</summary>
    internal int SlotIndex { get; init; }

    /// <summary>绑定无延迟的名称提示。</summary>
    public override void _Ready()
    {
        MouseEntered += () => Bag.ShowItemName(SlotIndex);
        MouseExited += Bag.HideItemName;
    }

    /// <summary>Ctrl＋左键自动穿戴；普通左键按下拿起或放置，松开保留拿起状态。</summary>
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion) { Bag.ShowItemName(SlotIndex); return; }
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
        if (click.Pressed)
        {
            if (click.CtrlPressed) Bag.AutoEquip(SlotIndex);
            else Bag.ClickItem(SlotIndex);
        }
        AcceptEvent();
    }
}
