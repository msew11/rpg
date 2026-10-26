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

/// <summary>角色窗口上方的可旋转预览及左右十一槽位，状态只取会话快照。</summary>
public partial class CharacterEquipmentControl : VBoxContainer
{
    /// <summary>跨窗口物品操作的背包控件。</summary>
    public BagControl Bag { get; init; } = null!;
    /// <summary>供旋转与外观展示复用的独立预览。</summary>
    private readonly RolePreviewControl _preview = new() { Name = "EquipmentPreview", CustomMinimumSize = new Vector2(245, 350) };
    /// <summary>每槽显示图标的固定控件。</summary>
    private readonly Dictionary<EquipmentSlot, TextureRect> _icons = new();
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

    /// <summary>左右严格保持用户给定的槽位顺序，中间放可旋转预览。</summary>
    public override void _Ready()
    {
        var row = new HBoxContainer { Name = "EquipmentLayout" };
        AddChild(row);
        var left = new VBoxContainer();
        row.AddChild(left);
        row.AddChild(_preview);
        var right = new VBoxContainer();
        row.AddChild(right);
        foreach (var entry in new[] { (EquipmentSlot.MainHand, "主手"), (EquipmentSlot.Head, "头"),
            (EquipmentSlot.Body, "身体"), (EquipmentSlot.Hands, "手"), (EquipmentSlot.Legs, "裤子"), (EquipmentSlot.Feet, "鞋子") })
            AddSlot(left, entry.Item1, entry.Item2);
        foreach (var entry in new[] { (EquipmentSlot.OffHand, "副手"), (EquipmentSlot.Face, "面"),
            (EquipmentSlot.Necklace, "项链"), (EquipmentSlot.Ring1, "戒指"), (EquipmentSlot.Ring2, "戒指") })
            AddSlot(right, entry.Item1, entry.Item2);
        AddChild(new Label { Text = "左键拿起或放置装备；右键卸下到背包", MouseFilter = MouseFilterEnum.Ignore });
        AddChild(_status);
        _subscription = GameServices.EventBus.Subscribe<EquipItemRt>(response =>
        {
            if (response.RoleId != GameServices.Session.RoleId) return;
            _status.Text = response.Success ? "" : response.Error;
            _status.Visible = !response.Success;
        });
        Refresh();
    }

    /// <summary>创建名称与图标独立排列的固定槽位，未开放槽位仍显示占位。</summary>
    private void AddSlot(VBoxContainer column, EquipmentSlot slot, string title)
    {
        var cell = new VBoxContainer();
        cell.AddThemeConstantOverride("separation", 0);
        column.AddChild(cell);
        var label = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 14);
        cell.AddChild(label);
        var panel = new EquipmentSlotControl { Name = $"Equipment{slot}", Bag = Bag, Slot = slot,
            CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Stop };
        var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.13f, 0.17f), BorderColor = new Color(0.35f, 0.40f, 0.48f) };
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
        cell.AddChild(panel);
        var icon = new TextureRect { Name = "EquipmentIcon", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(icon);
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
            ((Control)icon.GetParent()).TooltipText = cfg?.Name ?? (slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand ? "空槽" : "暂未开放");
        }
    }

    /// <summary>离开角色回收窗口自己的协议监听。</summary>
    public override void _ExitTree() { _subscription?.Dispose(); _subscription = null; }
}
