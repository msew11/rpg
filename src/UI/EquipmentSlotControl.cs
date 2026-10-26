using Godot;
using Rpg.Const;

namespace Rpg.UI;

/// <summary>装备槽的真实鼠标入口，左键拾取放置，Ctrl＋左键自动卸下。</summary>
public partial class EquipmentSlotControl : PanelContainer
{
    /// <summary>复用背包拥有的物品拿起上下文。</summary>
    public BagControl Bag { get; init; } = null!;
    /// <summary>此控件代表的枚举槽位。</summary>
    public EquipmentSlot Slot { get; init; }

    /// <summary>已穿装备与背包使用同一个即时信息浮窗。</summary>
    public override void _Ready()
    {
        MouseEntered += () => Bag.ShowEquipmentName(Slot, this);
        MouseExited += Bag.HideItemName;
        VisibilityChanged += () => { if (!IsVisibleInTree()) Bag.HideItemName(); };
    }

    /// <summary>消费整个鼠标操作，避免按下或松开穿透至世界。</summary>
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseMotion) { Bag.ShowEquipmentName(Slot, this); return; }
        if (input is not InputEventMouseButton click) return;
        if (click.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
        if (click.Pressed && Slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand)
        {
            if (click.ButtonIndex == MouseButton.Left)
            {
                if (click.CtrlPressed) Bag.Unequip(Slot);
                else Bag.ClickEquipment(Slot);
            }
        }
        AcceptEvent();
    }
}
