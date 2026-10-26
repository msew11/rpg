using Rpg.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Services;
using Rpg.Net.Message;
using Rpg.Const;

namespace Rpg.UI;

/// <summary>绘制权威背包快照并提交物品拖放，滚动网格适配小窗口。</summary>
public partial class BagControl : PanelContainer
{
    /// <summary>为缩放后的格子边界留出取整余量，避免完整背包出现滚动条。</summary>
    private const float FitAllowance = 4f;
    /// <summary>按基础配置列数排列的格子网格。</summary>
    private GridContainer _grid = null!;
    /// <summary>计算标题与网格之间的主题间隔。</summary>
    private VBoxContainer _column = null!;
    /// <summary>参与面板自然尺寸计算的标题栏。</summary>
    private HBoxContainer _header = null!;
    /// <summary>在视口不足时承载完整网格的滚动区。</summary>
    private ScrollContainer _scroll = null!;
    /// <summary>首次显示时加载的素材索引，不依赖 GM 图鉴窗口。</summary>
    private Dictionary<string, DataItemAsset>? _icons;
    /// <summary>已创建的固定格子，刷新只替换内容。</summary>
    private readonly List<PanelContainer> _slots = [];
    /// <summary>展示移动响应错误，不覆盖角色或背包缓存。</summary>
    private Label _moveStatus = null!;
    /// <summary>面板拥有的物品移动结果订阅。</summary>
    private IDisposable? _moveSubscription;
    /// <summary>数量确认结果订阅，与原移动响应一起释放。</summary>
    private IDisposable? _dropSubscription;
    /// <summary>穿脱错误与普通物品操作显示在同一反馈区。</summary>
    private IDisposable? _equipSubscription;
    /// <summary>地图点击弹出的数量确认，模态隔离世界输入。</summary>
    private ConfirmationDialog _dropDialog = null!;
    /// <summary>一至源数量的整数选择。</summary>
    private SpinBox _dropCount = null!;
    /// <summary>确认时再次验证的冻结上下文。</summary>
    private Variant _dropContext;
    /// <summary>冻结地图点击时的销毁意图，确认时不再读取键盘状态。</summary>
    private bool _destroyOnConfirm;
    /// <summary>刷新或关闭面板后使旧拖放上下文失效。</summary>
    private long _dragEpoch;
    /// <summary>独立于网格裁剪的物品与名称浮层。</summary>
    private CanvasLayer _itemLayer = null!;
    /// <summary>随鼠标移动的完整物品预览，空值表示未拿起。</summary>
    private Control? _itemPreview;
    /// <summary>拿起时冻结的角色、版本与实例。</summary>
    private Variant _pickedItem;
    /// <summary>没有默认等待时间的普通文本名称。</summary>
    private ItemNamePopup _itemName = null!;
    /// <summary>输入事件提供的视口坐标，不受世界摄像机或滚动影响。</summary>
    private Vector2 _pointerPosition;
    /// <summary>背包标题栏拖动与角色位置存储。</summary>
    private WindowDragControl _windowDrag = null!;

    /// <summary>创建标题、关闭按钮与双向滚动网格。</summary>
    public override void _Ready()
    {
        Name = "背包";
        MouseFilter = MouseFilterEnum.Stop;
        PopupPanelStyle.ApplyPadding(this);
        _column = new VBoxContainer();
        AddChild(_column);
        _header = new HBoxContainer { Name = "PanelHeader" };
        _column.AddChild(_header);
        _header.AddChild(PopupPanelStyle.CreateTitle("背包"));
        var close = new Button { Text = "关闭" };
        close.Pressed += Hide;
        _header.AddChild(close);
        _moveStatus = new Label { Name = "ItemMoveStatus", Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        _column.AddChild(_moveStatus);
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddChild(_scroll);
        _grid = new GridContainer { Name = "BagGrid" };
        _grid.AddThemeConstantOverride("h_separation", ItemSlotStyle.Spacing);
        _grid.AddThemeConstantOverride("v_separation", ItemSlotStyle.Spacing);
        _scroll.AddChild(_grid);
        _itemLayer = new CanvasLayer { Name = "ItemPointerLayer", Layer = 100 };
        AddChild(_itemLayer);
        _itemName = new ItemNamePopup { Name = "ItemNamePopup" };
        _itemLayer.AddChild(_itemName);
        VisibilityChanged += () => { _dragEpoch++; if (IsVisibleInTree()) Refresh(); else CancelItemDrag(); };
        _moveSubscription = GameServices.EventBus.Subscribe<MoveItemRt>(OnItemMoved);
        _dropSubscription = GameServices.EventBus.Subscribe<DropItemRt>(OnItemDropped);
        _equipSubscription = GameServices.EventBus.Subscribe<EquipItemRt>(response =>
        {
            if (response.RoleId == GameServices.Session.RoleId) ShowMoveStatus(response.Success ? "" : response.Error);
        });
        _dropDialog = new ConfirmationDialog { Name = "DropItemDialog", Title = "丢弃道具", Exclusive = true };
        _dropDialog.GetOkButton().Text = "丢弃";
        _dropDialog.GetCancelButton().Text = "取消";
        var form = new VBoxContainer();
        form.AddChild(new Label { Name = "DropItemPrompt", Text = "选择丢弃数量（丢在角色周围）" });
        _dropCount = new SpinBox { Name = "DropItemCount", MinValue = 1, MaxValue = 1, Step = 1, Rounded = true };
        form.AddChild(_dropCount);
        _dropDialog.AddChild(form);
        AddChild(_dropDialog);
        _dropDialog.Confirmed += ConfirmGroundDrop;
        _dropDialog.Canceled += CloseDropDialog;
        _dropDialog.CloseRequested += CloseDropDialog;
        _windowDrag = new WindowDragControl { Panel = this, Header = _header, SettingKey = "bag" };
        AddChild(_windowDrag);
        _Process(0);
    }

    /// <summary>按标题和完整网格收紧面板，受视口限制时补足滚动条空间。</summary>
    public override void _Process(double delta)
    {
        var mouse = _pointerPosition;
        if (_itemPreview is not null) _itemPreview.Position = mouse - _itemPreview.Size / 2;
        if (_itemName.Visible) _itemName.MoveTo(mouse);
        var viewport = GetViewportRect().Size;
        var grid = _grid.GetCombinedMinimumSize();
        var header = _header.GetCombinedMinimumSize();
        var content = grid + _scroll.GetThemeStylebox("panel").GetMinimumSize();
        var desired = new Vector2(Mathf.Max(header.X, content.X), header.Y + _column.GetThemeConstant("separation") + content.Y)
            + GetThemeStylebox("panel").GetMinimumSize() + new Vector2(FitAllowance, FitAllowance);
        if (_moveStatus.Visible) desired.Y += _moveStatus.GetCombinedMinimumSize().Y + _column.GetThemeConstant("separation");
        var available = (viewport - new Vector2(32, 32)).Max(Vector2.One);
        var vertical = desired.Y > available.Y;
        var horizontal = desired.X > available.X;
        var barWidth = _scroll.GetVScrollBar().GetCombinedMinimumSize().X;
        var barHeight = _scroll.GetHScrollBar().GetCombinedMinimumSize().Y;
        if (vertical && desired.X + barWidth > available.X) horizontal = true;
        if (horizontal && desired.Y + barHeight > available.Y) vertical = true;
        Size = (desired + new Vector2(vertical ? barWidth : 0, horizontal ? barHeight : 0)).Min(available);
        _windowDrag.ApplyPosition(new Vector2(Mathf.Min(Mathf.Max(432, (viewport.X - Size.X) / 2), Mathf.Max(16, viewport.X - Size.X - 16)),
            (viewport.Y - Size.Y) / 2));
    }

    /// <summary>读取最新只读缓存，按格子呈现图标、全部数量和名称提示。</summary>
    public void Refresh()
    {
        if (_grid is null || !Visible) return;
        HideItemName();
        CloseDropDialog();
        _dragEpoch++;
        CancelItemDrag();
        var config = GameServices.Config;
        var capacity = config.Basic.Value.GetBagCapacity(GameServices.Session.Role.BagExtraRows);
        _grid.Columns = config.Basic.Value.BagColumns;
        if (_slots.Count != capacity)
        {
            foreach (var slot in _slots) { _grid.RemoveChild(slot); slot.QueueFree(); }
            _slots.Clear();
            for (var index = 0; index < capacity; index++)
            {
                var slot = new BagSlotControl { Bag = this, SlotIndex = index,
                    Name = $"Slot{index}", MouseFilter = MouseFilterEnum.Stop };
                ItemSlotStyle.Apply(slot);
                _grid.AddChild(slot);
                _slots.Add(slot);
            }
        }
        foreach (var slot in _slots)
        {
            slot.TooltipText = "";
            foreach (var child in slot.GetChildren()) { slot.RemoveChild(child); child.QueueFree(); }
        }
        foreach (var item in GameServices.Session.Bag.Items)
        {
            var definition = config.Item.Items[item.ItemId];
            var slot = _slots[item.SlotIndex];
            var content = ItemSlotStyle.CreateContent();
            slot.AddChild(content);
            var texture = ResolveTexture(definition.IconAssetId);
            Control icon = texture is null
                ? new Label { Name = "MissingIcon", Text = "?", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : ItemSlotStyle.CreateIcon("ItemIcon");
            if (icon is TextureRect image) image.Texture = texture;
            content.AddChild(icon);
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            icon.MouseFilter = MouseFilterEnum.Ignore;
            var count = new Label { Name = "ItemCount", Text = item.Count.ToString(), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = MouseFilterEnum.Ignore };
            count.AddThemeColorOverride("font_outline_color", Colors.Black);
            count.AddThemeConstantOverride("outline_size", 4);
            content.AddChild(count);
            count.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
    }

    /// <summary>首次点击拿起，下一次点击校验并提交，然后释放预览。</summary>
    internal void ClickItem(int slotIndex)
    {
        if (_itemPreview is not null)
        {
            var data = _pickedItem;
            CancelItemDrag();
            UiSoundPlayer.Play(this, 1010);
            if (IsEquipmentPayload(data))
            {
                if (!IsCurrentEquipment(data)) { ShowMoveStatus("物品已更新，请重新选择装备"); return; }
                var equippedPayload = data.AsGodotDictionary();
                GameServices.Session.EquipItem(0, (EquipmentSlot)equippedPayload["equipment"].AsInt32(), equippedPayload["revision"].AsInt64(), slotIndex);
                return;
            }
            DropItem(data, slotIndex);
            return;
        }
        var payload = CreateItemDrag(slotIndex);
        if (payload.VariantType == Variant.Type.Nil) return;
        UiSoundPlayer.Play(this, 1001);
        _pickedItem = payload;
        HideItemName();
        _itemPreview = (Control)_slots[slotIndex].GetChild<Control>(0).Duplicate();
        _itemPreview.Name = "PickedItemPreview";
        _itemPreview.MouseFilter = MouseFilterEnum.Ignore;
        _itemPreview.Modulate = new Color(1, 1, 1, 0.85f);
        _itemLayer.AddChild(_itemPreview);
        _itemPreview.Size = _itemPreview.GetCombinedMinimumSize();
        _itemPreview.Position = _pointerPosition - _itemPreview.Size / 2;
    }

    /// <summary>只观察鼠标坐标，不消费输入，保证预览与硬件热点使用同一位置。</summary>
    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouse mouse) _pointerPosition = mouse.Position;
    }

    /// <summary>拿起后点击未被 UI 消费的地图，按 Ctrl 打开销毁确认，否则沿用丢弃确认，并阻止走路。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (_itemPreview is null || input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click) return;
        var data = _pickedItem;
        CancelItemDrag();
        if (IsEquipmentPayload(data)) { GetViewport().SetInputAsHandled(); return; }
        if (GameServices.World.HasRole && CanDropItem(data, 0))
        {
            var payload = data.AsGodotDictionary();
            var item = GameServices.Session.Bag.Items.Single(item => item.InstanceId == payload["instance"].AsInt64());
            if (item.Count == 1 && !click.CtrlPressed)
            {
                try { GameServices.Session.DropItem(item.InstanceId, 1, payload["revision"].AsInt64(),
                    position: GameServices.World.SelectGroundDropPosition()); }
                catch (Exception error) { ShowMoveStatus(error.Message); }
                GetViewport().SetInputAsHandled();
                return;
            }
            _dropContext = data;
            _destroyOnConfirm = click.CtrlPressed;
            _dropDialog.Title = _destroyOnConfirm ? "销毁道具" : "丢弃道具";
            _dropDialog.GetOkButton().Text = _destroyOnConfirm ? "销毁" : "丢弃";
            ((Label)_dropDialog.FindChild("DropItemPrompt", true, false)).Text = _destroyOnConfirm
                ? "确认销毁所选数量？永久删除，不会掉落地面。" : "选择丢弃数量（丢在角色周围）";
            _dropCount.MaxValue = item.Count;
            _dropCount.Value = item.Count;
            _dropDialog.PopupCentered(new Vector2I(300, 160));
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>指向有物品的格子时立即显示配置名称。</summary>
    internal void ShowItemName(int slotIndex)
    {
        if (_itemPreview is not null || !IsVisibleInTree()) return;
        var item = GameServices.Session.Bag.Items.FirstOrDefault(item => item.SlotIndex == slotIndex);
        if (item is null) { HideItemName(); return; }
        _itemName.ShowName(GameServices.Config.Item.Items[item.ItemId].Name, _pointerPosition);
    }

    /// <summary>离开物品或拿起时立即隐藏名称。</summary>
    internal void HideItemName() { if (_itemName is not null) _itemName.Hide(); }

    /// <summary>角色槽点击复用同一拿起状态，背包物品只在服务器确认后穿戴。</summary>
    internal void ClickEquipment(EquipmentSlot slot)
    {
        if (_itemPreview is not null)
        {
            var data = _pickedItem;
            CancelItemDrag();
            if (IsEquipmentPayload(data)) return;
            if (!CanDropItem(data, 0)) { ShowMoveStatus("物品已更新，请重新选择装备"); return; }
            var payload = data.AsGodotDictionary();
            var instance = payload["instance"].AsInt64();
            GameServices.Session.EquipItem(instance, slot, payload["revision"].AsInt64());
            UiSoundPlayer.Play(this, 1010);
            return;
        }
        var item = GameServices.Session.Bag.Equipment.FirstOrDefault(item => item.Slot == slot);
        if (item is null) return;
        Show();
        CancelItemDrag();
        _pickedItem = new Godot.Collections.Dictionary
        {
            ["panel"] = GetInstanceId(), ["epoch"] = _dragEpoch, ["role"] = GameServices.Session.RoleId,
            ["revision"] = GameServices.Session.Bag.Revision, ["instance"] = item.InstanceId, ["equipment"] = (int)slot
        };
        var texture = ResolveTexture(GameServices.Config.Item.Items[item.ItemId].IconAssetId);
        _itemPreview = new TextureRect { Name = "PickedItemPreview", Texture = texture, Size = new Vector2(46, 46),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
        _itemLayer.AddChild(_itemPreview);
        _itemPreview.Position = _pointerPosition - _itemPreview.Size / 2;
        UiSoundPlayer.Play(this, 1001);
    }

    /// <summary>Ctrl 快捷穿戴到既有推荐槽，替换仍由服务器原子处理。</summary>
    internal void AutoEquip(int slotIndex)
    {
        CancelItemDrag();
        if (!IsVisibleInTree() || !GameServices.Session.HasRole) return;
        var bag = GameServices.Session.Bag;
        var item = bag.Items.FirstOrDefault(item => item.SlotIndex == slotIndex);
        if (item is null) return;
        var cfg = GameServices.Config.Item.Items[item.ItemId];
        if (!EquipmentSlotUtil.TryGetSlot(cfg.Category, cfg.SubCategory, out var slot)) return;
        GameServices.Session.EquipItem(item.InstanceId, slot, bag.Revision);
        UiSoundPlayer.Play(this, 1010);
    }

    /// <summary>Ctrl 快捷卸下到首个空格，不使用乐观修改。</summary>
    internal void Unequip(EquipmentSlot slot)
    {
        CancelItemDrag();
        if (GameServices.Session.Bag.Equipment.All(item => item.Slot != slot)) return;
        GameServices.Session.EquipItem(0, slot, GameServices.Session.Bag.Revision);
    }

    /// <summary>辨认内部创建的装备来源载荷。</summary>
    private static bool IsEquipmentPayload(Variant data) => data.VariantType == Variant.Type.Dictionary &&
        data.AsGodotDictionary().ContainsKey("equipment");

    /// <summary>检查冻结装备仍属于当前角色、版本和拿起代次。</summary>
    private bool IsCurrentEquipment(Variant data)
    {
        if (!IsVisibleInTree() || !GameServices.Session.HasRole) return false;
        var payload = data.AsGodotDictionary();
        return payload["panel"].AsUInt64() == GetInstanceId() && payload["epoch"].AsInt64() == _dragEpoch &&
            payload["role"].AsInt64() == GameServices.Session.RoleId && payload["revision"].AsInt64() == GameServices.Session.Bag.Revision &&
            GameServices.Session.Bag.Equipment.Any(item => item.InstanceId == payload["instance"].AsInt64() &&
                (int)item.Slot == payload["equipment"].AsInt32());
    }

    /// <summary>关闭角色窗口时清除跨面板拿起状态。</summary>
    internal void CancelHeldItem() => CancelItemDrag();

    /// <summary>记录拖动开始时的角色、版本与实例，禁止空格创建拖放。</summary>
    internal Variant CreateItemDrag(int sourceSlot)
    {
        if (!IsVisibleInTree() || !GameServices.Session.HasRole) return default;
        var item = GameServices.Session.Bag.Items.FirstOrDefault(item => item.SlotIndex == sourceSlot);
        if (item is null) return default;
        ShowMoveStatus("");
        return new Godot.Collections.Dictionary
        {
            ["panel"] = GetInstanceId(), ["epoch"] = _dragEpoch,
            ["role"] = GameServices.Session.RoleId, ["revision"] = GameServices.Session.Bag.Revision,
            ["instance"] = item.InstanceId
        };
    }

    /// <summary>校验载荷来源与快照，不允许跨面板、跨角色或刷新前的拖放。</summary>
    internal bool CanDropItem(Variant data, int targetSlot)
    {
        if (!IsVisibleInTree() || !GameServices.Session.HasRole || targetSlot < 0 || targetSlot >= _slots.Count ||
            data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        foreach (var key in new[] { "panel", "epoch", "role", "revision", "instance" })
            if (!payload.ContainsKey(key) || payload[key].VariantType != Variant.Type.Int) return false;
        if (payload["panel"].AsUInt64() != GetInstanceId() || payload["role"].AsInt64() != GameServices.Session.RoleId) return false;
        if (payload["epoch"].AsInt64() != _dragEpoch || payload["revision"].AsInt64() != GameServices.Session.Bag.Revision)
        {
            ShowMoveStatus("背包已更新，请重新拖动物品");
            return false;
        }
        return
            GameServices.Session.Bag.Items.Any(item => item.InstanceId == payload["instance"].AsInt64());
    }

    /// <summary>只发送仍有效的整份物品移动，权威快照到达前保留原图标与数量。</summary>
    internal void DropItem(Variant data, int targetSlot)
    {
        if (!CanDropItem(data, targetSlot)) { ShowMoveStatus("背包已更新，请重新拖动物品"); return; }
        var payload = data.AsGodotDictionary();
        var instanceId = payload["instance"].AsInt64();
        if (GameServices.Session.Bag.Items.Single(item => item.InstanceId == instanceId).SlotIndex == targetSlot) return;
        try { GameServices.Session.MoveItem(instanceId, targetSlot, payload["revision"].AsInt64()); }
        catch (Exception error) { ShowMoveStatus(error.Message); }
    }

    /// <summary>物品移动失败时显示原因，成功状态由缓存阶段的完整推送提供。</summary>
    private void OnItemMoved(MoveItemRt response)
    {
        if (response.RoleId == GameServices.Session.RoleId) ShowMoveStatus(response.Success ? "" : response.Error);
    }

    /// <summary>确认时重新校验版本和整数输入，不使用刷新后的其他实例替代源物品。</summary>
    private void ConfirmGroundDrop()
    {
        var data = _dropContext;
        var destroy = _destroyOnConfirm;
        CloseDropDialog();
        if (!CanDropItem(data, 0)) { ShowMoveStatus("背包已更新，请重新选择丢弃物品"); return; }
        if (!int.TryParse(_dropCount.GetLineEdit().Text.Trim(), out var count) || count < 1 || count > _dropCount.MaxValue)
        { ShowMoveStatus("丢弃数量无效"); return; }
        var payload = data.AsGodotDictionary();
        try { GameServices.Session.DropItem(payload["instance"].AsInt64(), count, payload["revision"].AsInt64(), destroy,
            destroy ? null : GameServices.World.SelectGroundDropPosition()); }
        catch (Exception error) { ShowMoveStatus(error.Message); }
    }

    /// <summary>响应只显示反馈，两侧状态由 CacheBase 的已有通知处理。</summary>
    private void OnItemDropped(DropItemRt response)
    {
        if (response.RoleId != GameServices.Session.RoleId) return;
        if (response.Success && response.ObjectId.Length > 0) UiSoundPlayer.Play(this, 1010);
        ShowMoveStatus(response.Success ? "" : response.Error);
    }

    /// <summary>刷新、取消和离开时使确认上下文失效。</summary>
    private void CloseDropDialog()
    {
        _dropContext = default;
        _destroyOnConfirm = false;
        _dropDialog?.Hide();
    }

    /// <summary>以普通文本显示移动反馈，空文本不占用布局高度。</summary>
    private void ShowMoveStatus(string message)
    {
        _moveStatus.Text = message;
        _moveStatus.Visible = message.Length > 0;
    }

    /// <summary>HUD 离开时立即解除移动响应订阅并作废拖放。</summary>
    public void ReleaseSubscription()
    {
        CancelItemDrag();
        _dragEpoch++;
        _moveSubscription?.Dispose();
        _moveSubscription = null;
        _dropSubscription?.Dispose();
        _dropSubscription = null;
        _equipSubscription?.Dispose();
        _equipSubscription = null;
    }

    /// <summary>释放点选预览和名称，关闭及切角时幂等执行。</summary>
    private void CancelItemDrag()
    {
        CloseDropDialog();
        HideItemName();
        _itemPreview?.QueueFree();
        _itemPreview = null;
        _pickedItem = default;
    }

    /// <summary>离树时幂等回收订阅与拖动上下文。</summary>
    public override void _ExitTree() => ReleaseSubscription();

    /// <summary>背包独立持有素材索引，调用无状态图集裁剪工具。</summary>
    private AtlasTexture? ResolveTexture(string id)
    {
        _icons ??= ItemAtlasUtil.Index(ItemAtlasUtil.LoadCatalog());
        return ItemAtlasUtil.Resolve(_icons, id);
    }
}
