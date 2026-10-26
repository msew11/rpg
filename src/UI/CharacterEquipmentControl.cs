using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Utils;

namespace Rpg.UI;

/// <summary>角色窗口的五行装备布局，双手位于预览下方，状态只取会话快照。</summary>
public partial class CharacterEquipmentControl : VBoxContainer
{
    /// <summary>跨窗口物品操作的背包控件。</summary>
    public BagControl Bag { get; init; } = null!;
    /// <summary>供旋转与外观展示复用的独立预览。</summary>
    private readonly RolePreviewControl _preview = new() { Name = "EquipmentPreview", ShowPortrait = false,
        ActorScale = 1, CustomMinimumSize = new Vector2(160, 0) };
    /// <summary>每槽显示图标的固定控件。</summary>
    private readonly Dictionary<EquipmentSlot, TextureRect> _icons = new();
    /// <summary>空槽内的灰色名称，穿上装备后为图标让位。</summary>
    private readonly Dictionary<EquipmentSlot, Label> _slotNames = new();
    /// <summary>当前窗口拥有的素材索引。</summary>
    private Dictionary<string, DataItemAsset>? _assets;
    /// <summary>服务器错误在角色窗口内即时可见。</summary>
    private readonly Label _status = new() { Name = "EquipmentStatus", Visible = false,
        AutowrapMode = TextServer.AutowrapMode.WordSmart };
    /// <summary>窗口拥有的响应监听。</summary>
    private IDisposable? _subscription;
    /// <summary>避免位置推送重复解析装备图标和预览资源。</summary>
    private BagPbData? _lastBag;
    /// <summary>仅外观相关角色字段变化时刷新站立预览。</summary>
    private (long, RoleGender, int, string)? _lastAppearance;

    /// <summary>预览占前四行，双手横排与左侧第五行鞋子对齐。</summary>
    public override void _Ready()
    {
        var row = new HBoxContainer { Name = "EquipmentLayout" };
        row.AddThemeConstantOverride("separation", ItemSlotStyle.Spacing);
        AddChild(row);
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", ItemSlotStyle.Spacing);
        row.AddChild(left);
        var middle = new VBoxContainer();
        middle.AddThemeConstantOverride("separation", ItemSlotStyle.Spacing);
        row.AddChild(middle);
        _preview.CustomMinimumSize = new Vector2(_preview.CustomMinimumSize.X,
            ItemSlotStyle.SlotSize * 4 + ItemSlotStyle.Spacing * 3);
        middle.AddChild(_preview);
        var hands = new HBoxContainer { Name = "HandSlots", Alignment = BoxContainer.AlignmentMode.Center };
        hands.AddThemeConstantOverride("separation", ItemSlotStyle.Spacing);
        middle.AddChild(hands);
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", ItemSlotStyle.Spacing);
        row.AddChild(right);
        foreach (var entry in new[] { (EquipmentSlot.Head, "头"),
            (EquipmentSlot.Body, "身体"), (EquipmentSlot.Hands, "手"), (EquipmentSlot.Legs, "裤子"), (EquipmentSlot.Feet, "鞋子") })
            AddSlot(left, entry.Item1, entry.Item2);
        foreach (var entry in new[] { (EquipmentSlot.Face, "面"),
            (EquipmentSlot.Necklace, "项链"), (EquipmentSlot.Ring1, "戒指"), (EquipmentSlot.Ring2, "戒指") })
            AddSlot(right, entry.Item1, entry.Item2);
        AddSlot(hands, EquipmentSlot.MainHand, "主手");
        AddSlot(hands, EquipmentSlot.OffHand, "副手");
        AddChild(_status);
        _subscription = GameServices.EventBus.Subscribe<EquipItemRt>(response =>
        {
            if (response.RoleId != GameServices.Session.RoleId) return;
            _status.Text = response.Success ? "" : response.Error;
            _status.Visible = !response.Success;
        });
        Refresh();
    }

    /// <summary>名称与图标共用槽内区域，空槽用灰色文字标明部位。</summary>
    private void AddSlot(Container column, EquipmentSlot slot, string title)
    {
        var panel = new EquipmentSlotControl { Name = $"Equipment{slot}", Bag = Bag, Slot = slot,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop };
        ItemSlotStyle.Apply(panel);
        column.AddChild(panel);
        var label = new Label { Name = "SlotName", Text = title, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        panel.AddChild(label);
        _slotNames.Add(slot, label);
        var content = ItemSlotStyle.CreateContent();
        panel.AddChild(content);
        var icon = ItemSlotStyle.CreateIcon("EquipmentIcon");
        content.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _icons.Add(slot, icon);
    }

    /// <summary>整份权威物品更新后同时刷新槽位与预览，保留用户旋转朝向。</summary>
    public void Refresh()
    {
        if (!IsNodeReady() || !GameServices.Session.HasRole) return;
        var role = GameServices.Session.Role;
        var appearance = (role.RoleId, role.Gender, role.HeadId, role.Name);
        if (ReferenceEquals(_lastBag, GameServices.Session.Bag) && _lastAppearance == appearance) return;
        _lastBag = GameServices.Session.Bag;
        _lastAppearance = appearance;
        Bag.HideItemName();
        var equipment = GameServices.Session.Bag.Equipment;
        _preview.ShowRole(role.Gender, role.HeadId, role.RoleId, role.Name,
            mainHandId: equipment.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand)?.ItemId ?? 0,
            offHandId: equipment.FirstOrDefault(item => item.Slot == EquipmentSlot.OffHand)?.ItemId ?? 0);
        _assets ??= ItemAtlasUtil.Index(ItemAtlasUtil.LoadCatalog());
        foreach (var (slot, icon) in _icons)
        {
            var item = equipment.FirstOrDefault(item => item.Slot == slot);
            var cfg = item is null ? null : GameServices.Config.Item.Items[item.ItemId];
            icon.Texture = cfg is null ? null : ItemAtlasUtil.Resolve(_assets, cfg.IconAssetId);
            _slotNames[slot].Visible = item is null;
        }
    }

    /// <summary>离开角色回收窗口自己的协议监听。</summary>
    public override void _ExitTree() { _subscription?.Dispose(); _subscription = null; }
}
