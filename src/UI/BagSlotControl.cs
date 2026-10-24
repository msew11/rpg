using Godot;

namespace Rpg.UI;

/// <summary>背包格子的原生拖放入口，只将操作交给所属面板。</summary>
public partial class BagSlotControl : PanelContainer
{
    /// <summary>负责校验拖放上下文并提交请求的背包面板。</summary>
    internal BagControl Bag { get; init; } = null!;
    /// <summary>此格子在背包中的固定编号。</summary>
    internal int SlotIndex { get; init; }

    /// <summary>只有有效物品才开始拖动，并复制图标和数量作为鼠标预览。</summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var data = Bag.CreateItemDrag(SlotIndex);
        if (data.VariantType == Variant.Type.Nil) return data;
        var preview = (Control)GetChild<Control>(0).Duplicate();
        preview.Position = new Vector2(-24, -24);
        preview.Modulate = new Color(1, 1, 1, 0.85f);
        SetDragPreview(preview);
        return data;
    }

    /// <summary>只允许当前面板仍有效的物品上下文投放至容量内格子。</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data) => Bag.CanDropItem(data, SlotIndex);

    /// <summary>将鼠标释放转换为权威移动请求，不在格子内直接修改物品。</summary>
    public override void _DropData(Vector2 atPosition, Variant data) => Bag.DropItem(data, SlotIndex);
}
