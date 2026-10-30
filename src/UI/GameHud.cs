using System;
using System.Collections.Generic;
using Godot;
using System.Linq;
using Rpg.Services;
using Rpg.Protocol;
using Rpg.Settings.Data;
using Rpg.Config;

namespace Rpg.UI;

/// <summary>管理独立于 ECS 世界的 HUD、系统面板与本地设置界面。</summary>
public partial class GameHud : Control
{
    /// <summary>可调整位置的 HUD 元素。</summary>
    private readonly Dictionary<string, PanelContainer> _movable = new();
    /// <summary>HUD 元素上不参与排版的绿色调整覆盖层。</summary>
    private readonly Dictionary<string, HudEditOverlay> _handles = new();
    /// <summary>识别旧存档中尚未调整的默认坐标，按内容计算初始布局。</summary>
    private readonly Dictionary<string, HudPosition> _defaultPositions = RoleSetting.DefaultPositions();
    /// <summary>角色、背包、地图、GM 和系统弹出面板。</summary>
    private readonly List<Control> _panels = [];
    /// <summary>当前 HUD 拥有的服务器消息监听。</summary>
    private readonly List<IDisposable> _subscriptions = [];
    /// <summary>显示可恢复的网络或服务端错误。</summary>
    private Label _networkError = null!;
    /// <summary>显示角色头型与等级的头像。</summary>
    private AvatarControl _avatar = null!;
    /// <summary>头像右侧的角色名字。</summary>
    private Label _portraitName = null!;
    /// <summary>根据生命比例变色的状态条。</summary>
    private ProgressBar _healthBar = null!;
    /// <summary>显示当前魔力比例的状态条。</summary>
    private ProgressBar _manaBar = null!;
    /// <summary>生命条中央的当前值与上限。</summary>
    private Label _healthValue = null!;
    /// <summary>魔力条中央的当前值与上限。</summary>
    private Label _manaValue = null!;
    /// <summary>当前角色真实生命值，供临时预览换算。</summary>
    private int _currentHealth;
    /// <summary>当前角色生命上限，供临时预览换算。</summary>
    private int _maxHealth;
    /// <summary>仅在当前 HUD 生命周期生效的 GM 血条百分比。</summary>
    private double? _healthPreviewPercent;
    /// <summary>GM 面板中显示当前或预览生命百分比的输入框。</summary>
    private HSlider _healthPercentInput = null!;
    /// <summary>GM 滑动条右侧的百分比。</summary>
    private Label _healthPercentText = null!;
    /// <summary>角色面板中的详细状态文字。</summary>
    private Label _characterText = null!;
    /// <summary>角色上方的预览和装备槽位。</summary>
    private CharacterEquipmentControl _equipment = null!;
    /// <summary>大地图面板中的地图和位置文字。</summary>
    private Label _mapText = null!;
    /// <summary>地图画面右上角的雷达控件。</summary>
    private MiniMapControl _miniMap = null!;
    /// <summary>大地图面板中的地图控件。</summary>
    private MiniMapControl _largeMap = null!;
    /// <summary>第二条技能栏的显隐选项。</summary>
    private CheckBox _secondBar = null!;
    /// <summary>第三条技能栏的显隐选项。</summary>
    private CheckBox _thirdBar = null!;
    /// <summary>独立于系统设置的 HUD 调整操作窗口。</summary>
    private ViewportPanelControl _hudEditWindow = null!;
    /// <summary>调整期间持续显示的顶部快捷键提示。</summary>
    private Label _hudEditHint = null!;
    /// <summary>用于定位上拉菜单和恢复键盘焦点的系统按钮。</summary>
    private Button _systemButton = null!;
    /// <summary>菜单展开时消费外部点击的透明遮罩。</summary>
    private Control _systemMenuOverlay = null!;
    /// <summary>包含系统与角色设置及会话操作的上拉面板。</summary>
    private PanelContainer _systemMenu = null!;
    /// <summary>当前是否允许拖动 HUD。</summary>
    private bool _editing;
    /// <summary>调整期间独立于设置服务的坐标草稿。</summary>
    private Dictionary<string, HudPosition> _draftPositions = new();
    /// <summary>调整期间独立保存的聊天尺寸草稿。</summary>
    private DataHudSize _draftChatSize = new();
    /// <summary>常态右上角缩放入口，调整模式中隐藏。</summary>
    private ChatResizeHandle _chatResize = null!;
    /// <summary>正在拖动的 HUD 元素键。</summary>
    private string? _dragging;
    /// <summary>鼠标与被拖动 HUD 左上角的距离。</summary>
    private Vector2 _dragOffset;
    /// <summary>上次布局计算使用的视口尺寸。</summary>
    private Vector2 _lastViewport;
    /// <summary>延迟加载素材目录的独立道具图鉴。</summary>
    private ItemCatalogControl _itemCatalog = null!;
    /// <summary>读取最新角色缓存的只读背包面板。</summary>
    private BagControl _bag = null!;
    /// <summary>独立于弹出面板显隐的聊天输入与 GM 反馈。</summary>
    private ChatControl _chat = null!;
    /// <summary>角色设置窗口，用于关闭时取消快捷键监听。</summary>
    private Control _roleSettings = null!;
    /// <summary>关闭显示设置或离开世界时撤销未保存的窗口预览。</summary>
    private Action? _restoreDisplayPreview;
    /// <summary>关闭声音设置或离开世界时撤销未保存的试听音量。</summary>
    private Action? _restoreSoundPreview;
    /// <summary>三个快捷键按钮及其等待状态。</summary>
    private readonly Dictionary<string, Button> _shortcutButtons = new();
    /// <summary>最近成功保存的按钮文字，离开角色后取消监听也无需访问设置服务。</summary>
    private readonly Dictionary<string, string> _shortcutLabels = new();
    /// <summary>正在等待键盘输入的操作，空值表示未录入。</summary>
    private string? _capturingShortcut;
    /// <summary>快捷键范围、冲突或保存失败提示。</summary>
    private Label _shortcutHint = null!;

    /// <summary>需要返回角色选择界面时通知主控制器。</summary>
    public event Action? ChangeRoleRequested;
    /// <summary>需要返回登录页面时通知主控制器。</summary>
    public event Action? LoginRequested;
    /// <summary>需要关闭客户端时通知主控制器。</summary>
    public event Action? QuitRequested;
    /// <summary>通知世界表现切换地面物品信息框。</summary>
    public event Action<bool>? GroundItemInfoChanged;
    /// <summary>通知世界表现更新 GM 实体锚点调试样式。</summary>
    public event Action<bool, Color>? AnchorMarkersChanged;
    /// <summary>当前世界会话的地面物品信息显示状态。</summary>
    private bool _groundItemInfoVisible;

    /// <summary>从已注册服务读取选中角色，创建游戏界面及其监听。</summary>
    public override void _Ready()
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        BuildHud();
        BuildPanels();
        _chat = new ChatControl();
        AddChild(_chat);
        RegisterMovable("chat", "聊天框", _chat);
        _chatResize = new ChatResizeHandle { Panel = _chat };
        _chat.AddChild(_chatResize);
        BuildSystemMenu();
        BuildHudEditWindow();
        ApplyLayout();
        RefreshData();
        _networkError = Text("", 14);
        _networkError.Name = "NetworkError";
        _networkError.Position = new Vector2(16, 140);
        _networkError.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_networkError);

        _subscriptions.Add(GameServices.EventBus.Subscribe<Rpg.Protocol.WorldUpdateNotice>(_ => RefreshData()));
        _subscriptions.Add(GameServices.EventBus.Subscribe<Rpg.Protocol.RoleItemsChangedNotice>(notice =>
        { if (notice.RoleId == GameServices.Session.RoleId) { _bag.Refresh(); _equipment.Refresh(); } }));
    }

    /// <summary>退出界面时丢弃未确认的调整并释放会话订阅。</summary>
    public override void _ExitTree() => ReleaseSubscriptions();

    /// <summary>窗口或内容尺寸变化后重新收紧并排列 HUD。</summary>
    public override void _Process(double delta)
    {
        if (_chatResize.Resizing) return;
        var size = GetViewportRect().Size;
        if (size == _lastViewport)
        {
            var unchanged = true;
            foreach (var panel in _movable.Values)
            {
                var bounded = new Vector2(
                    Mathf.Clamp(panel.Position.X, 0, Mathf.Max(0, size.X - panel.Size.X)),
                    Mathf.Clamp(panel.Position.Y, 0, Mathf.Max(0, size.Y - panel.Size.Y)));
                if ((panel != _chat && !panel.Size.IsEqualApprox(panel.GetCombinedMinimumSize())) ||
                    !panel.Position.IsEqualApprox(bounded))
                { unchanged = false; break; }
            }
            if (unchanged) return;
        }
        ApplyLayout();
    }

    /// <summary>在整个视口内持续拖动 HUD，避免指针离开把手后中断。</summary>
    public override void _Input(InputEvent input)
    {
        if (_capturingShortcut is not null && input is InputEventKey captured)
        {
            GetViewport().SetInputAsHandled();
            if (!captured.Pressed || captured.Echo) return;
            if (captured.MetaPressed)
            { _shortcutHint.Text = "系统键不可使用，请重新输入。"; return; }
            try
            {
                if (!GameServices.Setting.TrySetShortcut(_capturingShortcut, DataShortcut.FromInput(captured), out var error))
                { _shortcutHint.Text = error; return; }
                _shortcutLabels[_capturingShortcut] = GameServices.Setting.GetShortcut(_capturingShortcut).DisplayName();
                CancelShortcutCapture();
                _shortcutHint.Text = "已保存快捷键。";
            }
            catch (Exception error) { _shortcutHint.Text = $"保存失败，请重试：{error.Message}"; }
            return;
        }
        if (_systemMenuOverlay.Visible && input is InputEventKey { Keycode: Key.Escape, Pressed: true })
        {
            UiSoundPlayer.Play(this, 1003);
            CloseSystemMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_editing && input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false })
        {
            if (_dragging is not null) _handles[_dragging].Dragging = false;
            _dragging = null;
            _hudEditWindow.Show();
            _hudEditWindow.FitToViewport();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!_editing || _dragging is null) return;
        if (input is InputEventMouseMotion motion)
        {
            MoveDraggedElement(_dragging, motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _handles[_dragging].Dragging = false;
            _dragging = null;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>文本与模态输入优先，普通游戏状态下只响应首次按下的窗口快捷键。</summary>
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (_editing || _capturingShortcut is not null || _systemMenuOverlay.Visible ||
            HasModalWindow() ||
            GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit ||
            input is not InputEventKey { Pressed: true, Echo: false, MetaPressed: false } key) return;
        var value = DataShortcut.FromInput(key);
        foreach (var (action, title) in new[] { ("character", "角色"), ("bag", "背包"), ("map", "大地图") })
        {
            if (GameServices.Setting.GetShortcut(action) != value) continue;
            TogglePanel(title);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (GameServices.Setting.GetShortcut("groundItems") != value) return;
        _groundItemInfoVisible = !_groundItemInfoVisible;
        GroundItemInfoChanged?.Invoke(_groundItemInfoVisible);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>模态确认框存在时保留它的键盘操作。</summary>
    private bool HasModalWindow()
    {
        foreach (var window in GetViewport().GetEmbeddedSubwindows())
            if (window.Visible && window.Exclusive) return true;
        return false;
    }

    /// <summary>窗口失焦结束抓取，保留尚未确认的布局草稿。</summary>
    public override void _Notification(int what)
    {
        if (what != NotificationApplicationFocusOut || _dragging is null) return;
        _handles[_dragging].Dragging = false;
        _dragging = null;
    }

    /// <summary>创建头像、小地图、三条九格技能栏和系统菜单。</summary>
    private void BuildHud()
    {
        var portrait = Movable("portrait", "角色头像");
        var backgroundStyle = (StyleBox) _movable["portrait"].GetThemeStylebox("panel").Duplicate();
        _movable["portrait"].AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var portraitStage = new Control { Name = "PortraitStage", CustomMinimumSize = new Vector2(216, 66) };
        portrait.AddChild(portraitStage);
        var statusBackground = new Panel { Name = "PortraitBackground", Position = new Vector2(40, 0),
            Size = new Vector2(176, 57), MouseFilter = MouseFilterEnum.Ignore };
        statusBackground.AddThemeStyleboxOverride("panel", backgroundStyle);
        portraitStage.AddChild(statusBackground);
        _avatar = new AvatarControl { Name = "Avatar", CustomMinimumSize = new Vector2(72, 72),
            Scale = new Vector2(0.9f, 0.9f), MouseFilter = MouseFilterEnum.Ignore };
        portraitStage.AddChild(_avatar);
        var status = new VBoxContainer { Name = "PortraitStatus", Position = new Vector2(72, 3),
            Size = new Vector2(140, 50), MouseFilter = MouseFilterEnum.Ignore };
        status.AddThemeConstantOverride("separation", 2);
        portraitStage.AddChild(status);
        _portraitName = Text("", 13);
        _portraitName.Name = "PortraitName";
        _portraitName.HorizontalAlignment = HorizontalAlignment.Center;
        status.AddChild(_portraitName);
        _healthBar = CreateStatusBar("HealthBar", new Color("3ca66d"));
        status.AddChild(_healthBar);
        _healthValue = CreateBarValue("HealthValue", _healthBar);
        _manaBar = CreateStatusBar("ManaBar", new Color("418ec4"));
        status.AddChild(_manaBar);
        _manaValue = CreateBarValue("ManaValue", _manaBar);

        var radarBox = Movable("radar", "小地图");
        _movable["radar"].Name = "Radar";
        _movable["radar"].AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _miniMap = new MiniMapControl { CustomMinimumSize = new Vector2(150, 145) };
        radarBox.AddChild(_miniMap);

        var slotStyle = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(0.45f, 0.55f, 0.65f, 0.85f) };
        slotStyle.SetBorderWidthAll(1);
        for (var bar = 1; bar <= 3; bar++)
        {
            var panel = Movable($"hotbar{bar}", $"技能栏 {bar}");
            // 复用原底色绘制黑色外框，中心不填充以保留格内透明。
            var barStyle = (StyleBoxFlat)_movable[$"hotbar{bar}"].GetThemeStylebox("panel").Duplicate();
            barStyle.BorderColor = barStyle.BgColor;
            barStyle.DrawCenter = false;
            barStyle.SetBorderWidthAll(4);
            barStyle.SetContentMarginAll(4);
            _movable[$"hotbar{bar}"].AddThemeStyleboxOverride("panel", barStyle);
            var row = new HBoxContainer();
            panel.AddChild(row);
            for (var slot = 1; slot <= 9; slot++)
            {
                var cell = new PanelContainer { CustomMinimumSize = new Vector2(32, 32) };
                cell.AddThemeStyleboxOverride("panel", slotStyle);
                row.AddChild(cell);
            }
            _movable[$"hotbar{bar}"].Visible = GameServices.Setting.IsHotbarVisible(bar);
        }

        var menu = Movable("menu", "系统菜单");
        var menuRow = new HBoxContainer();
        menu.AddChild(menuRow);
        foreach (var title in new[] { "角色", "背包", "大地图", "GM", "系统" })
        {
            var button = new Button { Text = title, CustomMinimumSize = new Vector2(62, 34) };
            if (title == "系统")
            {
                _systemButton = button;
                button.Pressed += ToggleSystemMenu;
            }
            else button.Pressed += () => TogglePanel(title);
            menuRow.AddChild(button);
        }
    }

    /// <summary>创建位于所有普通面板之上的系统菜单与点击遮罩。</summary>
    private void BuildSystemMenu()
    {
        _systemMenuOverlay = new Control { Name = "SystemMenuOverlay", Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_systemMenuOverlay);
        _systemMenuOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _systemMenuOverlay.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true }) return;
            _systemMenuOverlay.AcceptEvent();
            UiSoundPlayer.Play(this, 1003);
            CloseSystemMenu();
        };
        _systemMenu = new PanelContainer { Name = "SystemMenu", CustomMinimumSize = new Vector2(180, 0), MouseFilter = MouseFilterEnum.Stop };
        _systemMenuOverlay.AddChild(_systemMenu);
        var entries = new VBoxContainer();
        _systemMenu.AddChild(entries);
        AddSystemMenuItem(entries, "系统设置", () => TogglePanel("系统设置"));
        AddSystemMenuItem(entries, "角色设置", () => TogglePanel("角色设置"));
        AddSystemMenuItem(entries, "返回角色选择", () => ChangeRoleRequested?.Invoke());
        AddSystemMenuItem(entries, "返回登陆", () => LoginRequested?.Invoke());
        AddSystemMenuItem(entries, "退出游戏", () => QuitRequested?.Invoke());
    }

    /// <summary>添加先收起菜单再执行操作的条目。</summary>
    private void AddSystemMenuItem(VBoxContainer entries, string title, Action action)
    {
        var button = new Button { Text = title, CustomMinimumSize = new Vector2(0, 34) };
        button.Pressed += () =>
        {
            CloseSystemMenu();
            action();
        };
        entries.AddChild(button);
    }

    /// <summary>在系统按钮上方展开菜单，并把面板约束在当前视口内。</summary>
    private void ToggleSystemMenu()
    {
        if (_systemMenuOverlay.Visible)
        {
            CloseSystemMenu();
            return;
        }
        PositionSystemMenu();
        _systemMenuOverlay.Show();
    }

    /// <summary>布局刷新时只更新菜单位置，保持展开状态及当前焦点。</summary>
    private void PositionSystemMenu()
    {
        _systemMenu.Size = _systemMenu.GetCombinedMinimumSize();
        var button = _systemButton.GetGlobalRect();
        var desired = new Vector2(button.End.X - _systemMenu.Size.X, button.Position.Y - _systemMenu.Size.Y - 4) - GlobalPosition;
        var viewport = GetViewportRect().Size;
        _systemMenu.Position = new Vector2(
            Mathf.Clamp(desired.X, 0, Mathf.Max(0, viewport.X - _systemMenu.Size.X)),
            Mathf.Clamp(desired.Y, 0, Mathf.Max(0, viewport.Y - _systemMenu.Size.Y)));
    }

    /// <summary>收起系统菜单并释放入口焦点，避免残留高亮。</summary>
    private void CloseSystemMenu()
    {
        if (!_systemMenuOverlay.Visible) return;
        _systemMenuOverlay.Hide();
        _systemButton.ReleaseFocus();
    }

    /// <summary>创建一个可在 HUD 编辑模式拖动的元素。</summary>
    private VBoxContainer Movable(string key, string title)
    {
        var panel = new PanelContainer { Name = key };
        AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        RegisterMovable(key, title, panel);
        return box;
    }

    /// <summary>注册整区可拖动的绿色覆盖框，不占用容器布局。</summary>
    private void RegisterMovable(string key, string title, PanelContainer panel)
    {
        var handle = new HudEditOverlay { Name = "HudEditOverlay", Title = title, TopLevel = true, Visible = false };
        handle.GuiInput += input => HandleDrag(key, input);
        panel.AddChild(handle);
        _movable[key] = panel;
        _handles[key] = handle;
    }

    /// <summary>创建业务窗口及独立的系统、角色设置页签。</summary>
    private void BuildPanels()
    {
        var character = CreatePanel("角色", Vector2.Zero);
        _characterText = Text("", 14);

        _bag = new BagControl { Name = "背包", Visible = false };
        AddChild(_bag);
        _panels.Add(_bag);
        _equipment = new CharacterEquipmentControl { Name = "CharacterEquipment", Bag = _bag };
        character.AddChild(_equipment);
        character.AddChild(_characterText);
        var characterWindow = (Control)character.GetParent().GetParent().GetParent();
        characterWindow.VisibilityChanged += () => { if (!characterWindow.Visible) _bag.CancelHeldItem(); };

        var mapPanel = CreatePanel("大地图", new Vector2(440, 440));
        _mapText = Text("", 14);
        mapPanel.AddChild(_mapText);
        _largeMap = new MiniMapControl { CustomMinimumSize = new Vector2(390, 340) };
        mapPanel.AddChild(_largeMap);

        var gm = CreatePanel("GM", new Vector2(400, 0));
        var consoleLog = new CheckButton
        {
            Name = "ConsoleLogEnabled", Text = "控制台日志",
            ButtonPressed = GameServices.Env.ConsoleLogEnabled
        };
        consoleLog.Toggled += enabled => SetConsoleLogging(consoleLog, enabled);
        gm.AddChild(consoleLog);
        var anchorRow = new HBoxContainer { Name = "EntityAnchorControls" };
        var showAnchors = new CheckButton
        {
            Name = "ShowEntityAnchors", Text = "显示物体锚点",
            ButtonPressed = GameServices.Env.ShowEntityAnchors
        };
        var anchorColor = new ColorPickerButton
        {
            Name = "EntityAnchorColor", Color = Colors.Yellow,
            CustomMinimumSize = new Vector2(42, 32), TooltipText = "物体锚点颜色"
        };
        showAnchors.Toggled += visible => SetAnchorMarkersVisible(showAnchors, visible, anchorColor.Color);
        anchorColor.ColorChanged += color => AnchorMarkersChanged?.Invoke(showAnchors.ButtonPressed, color);
        anchorRow.AddChild(showAnchors);
        anchorRow.AddChild(anchorColor);
        gm.AddChild(anchorRow);
        var flushRow = new HBoxContainer();
        flushRow.AddChild(new Label { Text = "Server 落地检测（秒，0 关闭）" });
        var flushInterval = new SpinBox
        {
            Name = "ServerFlushIntervalSeconds", MinValue = 0, MaxValue = 2147483645,
            Step = 5, Value = GameServices.Env.ServerFlushIntervalSeconds,
            CustomMinimumSize = new Vector2(100, 0)
        };
        flushInterval.ValueChanged += seconds => SetServerFlushInterval(flushInterval, seconds);
        flushRow.AddChild(flushInterval);
        gm.AddChild(flushRow);
        var speedRow = new HBoxContainer { Name = "MovementSpeedControls" };
        speedRow.AddChild(new Label { Text = "移动速度" });
        var speedSlider = new HSlider
        {
            Name = "MovementSpeed", MinValue = EnvService.MinimumMovementSpeed,
            MaxValue = EnvService.MaximumMovementSpeed, Step = 10,
            Value = GameServices.Env.MovementSpeed,
            CustomMinimumSize = new Vector2(150, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var speedValue = new Label { Name = "MovementSpeedValue", Text = $"{speedSlider.Value:0}",
            CustomMinimumSize = new Vector2(42, 0), HorizontalAlignment = HorizontalAlignment.Right };
        speedSlider.ValueChanged += value => SetMovementSpeed(speedSlider, speedValue, value);
        speedRow.AddChild(speedSlider);
        speedRow.AddChild(speedValue);
        gm.AddChild(speedRow);
        var healthRow = new HBoxContainer { Name = "HealthPreviewControls" };
        healthRow.AddChild(new Label { Text = "血条预览（%）" });
        _healthPercentInput = new HSlider
        {
            Name = "HealthPercent", MinValue = 0, MaxValue = 100, Step = 1,
            Value = HealthPercent(GameServices.Session.Role.Health, GameServices.Session.Role.MaxHealth),
            CustomMinimumSize = new Vector2(150, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _healthPercentText = new Label { Name = "HealthPercentText", CustomMinimumSize = new Vector2(46, 0),
            HorizontalAlignment = HorizontalAlignment.Right, Text = $"{_healthPercentInput.Value:0}%" };
        _healthPercentInput.ValueChanged += value =>
        {
            _healthPreviewPercent = value;
            _healthPercentText.Text = $"{value:0}%";
            UpdateHealthBar(value);
        };
        healthRow.AddChild(_healthPercentInput);
        healthRow.AddChild(_healthPercentText);
        gm.AddChild(healthRow);
        var gmActions = new HBoxContainer { Name = "GmActions" };
        gm.AddChild(gmActions);
        var openUserData = new Button
        {
            Name = "OpenUserDataDirectory",
            Text = "打开用户数据目录",
            TooltipText = "存放本地设置和模拟服务端存档的目录",
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        openUserData.Pressed += OpenUserDataDirectory;
        gmActions.AddChild(openUserData);
        _itemCatalog = new ItemCatalogControl { Name = "ItemCatalog", Visible = false };
        AddChild(_itemCatalog);
        _panels.Add(_itemCatalog);
        var openCatalog = new Button
        {
            Name = "OpenItemCatalog", Text = "查看道具图鉴",
            CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        openCatalog.Pressed += () =>
        {
            foreach (var panel in _panels.Where(panel => !IsIndependentWindow(panel))) panel.Hide();
            _itemCatalog.Open();
            MoveChild(_itemCatalog, GetChildCount() - 1);
        };
        gmActions.AddChild(openCatalog);

        var system = CreatePanel("系统设置", new Vector2(380, 390));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(345, 295) };
        system.AddChild(tabs);
        BuildDisplayTab(tabs, GetNode<Control>("系统设置"));
        BuildSoundTab(tabs);
        var role = CreatePanel("角色设置", new Vector2(440, 0));
        _roleSettings = GetNode<Control>("角色设置");
        var roleTabs = new TabContainer { Name = "RoleSettingTabs", CustomMinimumSize = new Vector2(360, 295) };
        role.AddChild(roleTabs);
        BuildHudTab(roleTabs);
        BuildShortcutTab(roleTabs);
        _roleSettings.VisibilityChanged += () => { if (!_roleSettings.Visible) CancelShortcutCapture(); };
        roleTabs.TabChanged += _ => CancelShortcutCapture();
    }

    /// <summary>保存检测秒数并立即供服务端读取，失败恢复输入并显示原因。</summary>
    private void SetServerFlushInterval(SpinBox input, double seconds)
    {
        try
        {
            if (seconds != Math.Truncate(seconds)) throw new ArgumentException("检测间隔必须为整数秒");
            GameServices.Env.ServerFlushIntervalSeconds = checked((int)seconds);
            _networkError.Text = "";
        }
        catch (Exception error)
        {
            input.SetValueNoSignal(GameServices.Env.ServerFlushIntervalSeconds);
            _networkError.Text = $"无法保存落地检测设置：{error.Message}";
        }
    }

    /// <summary>保存移动速度，失败时恢复滑条和数值并显示错误。</summary>
    private void SetMovementSpeed(HSlider slider, Label label, double speed)
    {
        try
        {
            GameServices.Env.MovementSpeed = checked((int)speed);
            label.Text = $"{GameServices.Env.MovementSpeed}";
            _networkError.Text = "";
        }
        catch (Exception error)
        {
            slider.SetValueNoSignal(GameServices.Env.MovementSpeed);
            label.Text = $"{GameServices.Env.MovementSpeed}";
            _networkError.Text = $"无法保存移动速度：{error.Message}";
        }
    }

    /// <summary>保存 .env 日志标记；失败恢复开关并在 HUD 显示原因。</summary>
    private void SetConsoleLogging(CheckButton toggle, bool enabled)
    {
        try
        {
            GameServices.Env.ConsoleLogEnabled = enabled;
            _networkError.Text = "";
        }
        catch (Exception error)
        {
            toggle.SetPressedNoSignal(GameServices.Env.ConsoleLogEnabled);
            _networkError.Text = $"无法保存 .env 日志设置：{error.Message}";
        }
    }

    /// <summary>保存锚点显示状态，成功后更新世界；失败时恢复控件并提示。</summary>
    private void SetAnchorMarkersVisible(CheckButton toggle, bool visible, Color color)
    {
        try
        {
            GameServices.Env.ShowEntityAnchors = visible;
            AnchorMarkersChanged?.Invoke(visible, color);
            _networkError.Text = "";
        }
        catch (Exception error)
        {
            toggle.SetPressedNoSignal(GameServices.Env.ShowEntityAnchors);
            _networkError.Text = $"无法保存 .env 锚点设置：{error.Message}";
        }
    }

    /// <summary>动态解析应用用户数据根目录，在系统文件管理器中打开其内容。</summary>
    private void OpenUserDataDirectory()
    {
        var error = OS.ShellShowInFileManager(ProjectSettings.GlobalizePath("user://"), true);
        _networkError.Text = error == Error.Ok ? "" : $"无法打开用户数据目录：{error}";
    }

    /// <summary>创建标题居左、关闭按钮靠右的弹出面板内容列。</summary>
    private VBoxContainer CreatePanel(string title, Vector2 minimum)
    {
        var outer = new ViewportPanelControl
        {
            Name = title, Title = title, PreferredWidth = minimum.X, Visible = false,
            UseContentPadding = true,
            WindowSettingKey = title == "角色" ? "character" : title == "大地图" ? "map" : ""
        };
        AddChild(outer);
        _panels.Add(outer);
        return outer.Content;
    }

    /// <summary>先选择显示模式，再按模式提供普通窗口分辨率或当前屏幕分辨率。</summary>
    private void BuildDisplayTab(TabContainer tabs, Control systemPanel)
    {
        var page = new VBoxContainer { Name = "显示" };
        tabs.AddChild(page);
        page.AddChild(Text("显示模式", 16));
        var modes = new OptionButton();
        modes.AddItem("窗口", (int)WindowDisplayMode.Windowed);
        modes.AddItem("全屏", (int)WindowDisplayMode.Fullscreen);
        modes.AddItem("无边框窗口全屏", (int)WindowDisplayMode.BorderlessFullscreen);
        modes.Select((int)GameServices.Setting.WindowMode);
        page.AddChild(modes);
        page.AddChild(Text("分辨率", 16));
        var options = new OptionButton();
        page.AddChild(options);
        var gameWindow = GetWindow();
        var sizes = new List<Vector2I>
        {
            new Vector2I(800, 600), new Vector2I(1024, 768), new Vector2I(1280, 720),
            new Vector2I(1280, 800), new Vector2I(1366, 768), new Vector2I(1440, 900),
            new Vector2I(1600, 900), new Vector2I(1920, 1080), new Vector2I(2560, 1440),
            new Vector2I(3840, 2160)
        };
        var savedSize = new Vector2I(GameServices.Setting.Width, GameServices.Setting.Height);
        var savedMode = GameServices.Setting.WindowMode;
        var windowSize = savedSize;
        if (!sizes.Contains(windowSize)) sizes.Add(windowSize);

        // 模式改变时保留窗口尺寸，全屏只展示当前显示器尺寸。
        void UpdateResolutionOptions(long modeIndex)
        {
            options.Clear();
            if (modeIndex == (long)WindowDisplayMode.Windowed)
            {
                for (var i = 0; i < sizes.Count; i++) options.AddItem($"{sizes[i].X} × {sizes[i].Y}", i);
                options.Select(sizes.IndexOf(windowSize));
                options.Disabled = false;
                return;
            }

            var screenSize = DisplayServer.ScreenGetSize(gameWindow.CurrentScreen);
            if (screenSize.X <= 0 || screenSize.Y <= 0) screenSize = gameWindow.Size;
            options.AddItem($"{screenSize.X} × {screenSize.Y}");
            options.Select(0);
            options.Disabled = true;
        }
        // 预览只修改 Godot 窗口，不写入系统设置。
        void PreviewDisplay()
        {
            DisplaySettings.Apply(gameWindow, windowSize, (WindowDisplayMode)modes.Selected);
            ApplyLayout();
        }

        // 关闭面板或离开世界时以最近一次保存的设置恢复窗口与控件。
        void RestoreDisplayPreview()
        {
            if (windowSize != savedSize || modes.Selected != (int)savedMode)
                DisplaySettings.Apply(gameWindow, savedSize, savedMode);
            windowSize = savedSize;
            modes.Select((int)savedMode);
            UpdateResolutionOptions(modes.Selected);
            if (IsInsideTree()) ApplyLayout();
        }

        options.ItemSelected += index =>
        {
            if (modes.Selected != (int)WindowDisplayMode.Windowed) return;
            windowSize = sizes[(int)index];
            PreviewDisplay();
        };
        modes.ItemSelected += index =>
        {
            PreviewDisplay();
            UpdateResolutionOptions(index);
        };
        UpdateResolutionOptions(modes.Selected);
        systemPanel.VisibilityChanged += () =>
        {
            if (!systemPanel.Visible) RestoreDisplayPreview();
        };
        _restoreDisplayPreview = RestoreDisplayPreview;
        var save = new Button { Text = "保存" };
        save.Pressed += () =>
        {
            try
            {
                var mode = (WindowDisplayMode)modes.Selected;
                GameServices.Setting.SetDisplay(windowSize.X, windowSize.Y, mode);
                savedSize = windowSize;
                savedMode = mode;
                _networkError.Text = "";
            }
            catch (Exception error) { _networkError.Text = $"无法保存显示设置：{error.Message}"; }
        };
        page.AddChild(save);
    }

    /// <summary>分别试听 BGM 与音效，并在保存时一次提交两路音量。</summary>
    private void BuildSoundTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "声音" };
        tabs.AddChild(page);
        page.AddChild(Text("BGM 音量", 16));
        var bgm = new HSlider { Name = "BgmVolume", MinValue = 0, MaxValue = 100, Step = 1,
            Value = GameServices.Setting.MasterVolume * 100,
            CustomMinimumSize = new Vector2(280, 28) };
        page.AddChild(bgm);
        page.AddChild(Text("音效音量", 16));
        var effects = new HSlider { Name = "EffectsVolume", MinValue = 0, MaxValue = 100, Step = 1,
            Value = GameServices.Setting.EffectsVolume * 100, CustomMinimumSize = new Vector2(280, 28) };
        page.AddChild(effects);
        var savedBgm = GameServices.Setting.MasterVolume;
        var savedEffects = GameServices.Setting.EffectsVolume;
        void Preview() => UiSoundPlayer.SetVolumes((float)bgm.Value / 100, (float)effects.Value / 100);
        void Restore()
        {
            bgm.SetValueNoSignal(savedBgm * 100);
            effects.SetValueNoSignal(savedEffects * 100);
            UiSoundPlayer.SetVolumes(savedBgm, savedEffects);
        }
        bgm.ValueChanged += _ => Preview();
        effects.ValueChanged += _ => Preview();
        var systemPanel = GetNode<Control>("系统设置");
        systemPanel.VisibilityChanged += () => { if (!systemPanel.Visible) Restore(); };
        _restoreSoundPreview = Restore;
        var save = new Button { Text = "保存" };
        save.Pressed += () =>
        {
            try
            {
                GameServices.Setting.SetVolumes((float)bgm.Value / 100, (float)effects.Value / 100);
                savedBgm = GameServices.Setting.MasterVolume;
                savedEffects = GameServices.Setting.EffectsVolume;
                _networkError.Text = "";
            }
            catch (Exception error) { _networkError.Text = $"无法保存声音设置：{error.Message}"; }
        };
        page.AddChild(save);
    }

    /// <summary>加入技能栏显隐和 HUD 拖动调整入口。</summary>
    private void BuildHudTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "界面" };
        tabs.AddChild(page);
        page.AddChild(Text("技能栏 1 始终显示", 14));
        _secondBar = new CheckBox { Text = "显示技能栏 2", ButtonPressed = GameServices.Setting.IsHotbarVisible(2) };
        _thirdBar = new CheckBox { Text = "显示技能栏 3", ButtonPressed = GameServices.Setting.IsHotbarVisible(3) };
        page.AddChild(_secondBar);
        page.AddChild(_thirdBar);
        _secondBar.Toggled += enabled => SetBarVisible(2, enabled);
        _thirdBar.Toggled += enabled => SetBarVisible(3, enabled);
        var edit = new Button { Text = "进入 HUD 调整" };
        edit.Pressed += BeginEdit;
        page.AddChild(edit);
        var reset = new Button { Text = "恢复默认布局" };
        reset.Pressed += ResetHudLayout;
        page.AddChild(reset);
    }

    /// <summary>创建按角色保存的窗口及地面信息快捷键。</summary>
    private void BuildShortcutTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "快捷键" };
        tabs.AddChild(page);
        foreach (var (key, title) in new[] { ("character", "角色"), ("bag", "道具"), ("map", "地图"), ("groundItems", "地面物品信息") })
        {
            var row = new HBoxContainer();
            page.AddChild(row);
            row.AddChild(new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var button = new Button
            {
                Name = "Shortcut_" + key, Text = GameServices.Setting.GetShortcut(key).DisplayName(),
                ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(180, 36)
            };
            _shortcutButtons[key] = button;
            _shortcutLabels[key] = button.Text;
            button.Pressed += () =>
            {
                CancelShortcutCapture();
                _capturingShortcut = key;
                button.SetPressedNoSignal(true);
                button.Text = "请按快捷键…";
                _shortcutHint.Text = "等待键盘输入；非法按键会继续等待。";
            };
            row.AddChild(button);
        }
        _shortcutHint = Text("支持字母、数字、F1–F12，可组合 Ctrl / Alt / Shift。", 14);
        _shortcutHint.Name = "ShortcutHint";
        _shortcutHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        page.AddChild(_shortcutHint);
    }

    /// <summary>结束监听并清除所有绑定高亮，恢复已保存的键名。</summary>
    private void CancelShortcutCapture()
    {
        _capturingShortcut = null;
        foreach (var (key, button) in _shortcutButtons)
        {
            button.SetPressedNoSignal(false);
            button.Text = _shortcutLabels[key];
            button.ReleaseFocus();
        }
    }

    /// <summary>创建可独立关闭的调整操作窗，以及不拦截输入的顶部提示。</summary>
    private void BuildHudEditWindow()
    {
        _hudEditHint = Text("按 ESC 呼出 HUD 调整窗口", 16);
        _hudEditHint.Name = "HudEditHint";
        _hudEditHint.Visible = false;
        _hudEditHint.AnchorRight = 1;
        _hudEditHint.OffsetTop = 8;
        _hudEditHint.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_hudEditHint);
        _hudEditWindow = new ViewportPanelControl
        {
            Name = "HudEditWindow", Title = "HUD 调整", PreferredWidth = 260, Visible = false
        };
        AddChild(_hudEditWindow);
        var complete = new Button { Text = "完成" };
        complete.Pressed += CompleteEdit;
        _hudEditWindow.Content.AddChild(complete);
        var cancel = new Button { Text = "退出" };
        cancel.Pressed += CancelEdit;
        _hudEditWindow.Content.AddChild(cancel);
        var reset = new Button { Text = "恢复" };
        reset.Pressed += ResetHudLayout;
        _hudEditWindow.Content.AddChild(reset);
    }

    /// <summary>调整期间仅重置位置草稿，否则立即保存默认布局。</summary>
    private void ResetHudLayout()
    {
        if (_editing)
        {
            _draftPositions = RoleSetting.DefaultPositions();
            _draftChatSize = new DataHudSize();
        }
        else GameServices.Setting.ResetHudPositions();
        ApplyLayout();
    }

    /// <summary>根据角色设置显示或隐藏指定技能栏。</summary>
    private void SetBarVisible(int index, bool visible)
    {
        GameServices.Setting.SetHotbarVisible(index, visible);
        _movable[$"hotbar{index}"].Visible = visible;
    }

    /// <summary>复制布局草稿，关闭系统设置并进入调整模式。</summary>
    private void BeginEdit()
    {
        if (_editing) return;
        CloseSystemMenu();
        _draftPositions.Clear();
        _draftChatSize = GameServices.Setting.GetChatSize();
        foreach (var key in _movable.Keys) _draftPositions[key] = GameServices.Setting.GetHudPosition(key);
        foreach (var panel in _panels) panel.Hide();
        SetEditing(true);
    }

    /// <summary>保存当前布局草稿后结束调整模式。</summary>
    private void CompleteEdit()
    {
        if (!_editing) return;
        foreach (var (key, position) in _draftPositions)
            GameServices.Setting.SetHudPosition(key, position.X, position.Y);
        GameServices.Setting.SetChatSize(_draftChatSize.Width, _draftChatSize.Height);
        GameServices.Setting.SaveCurrentRole();
        SetEditing(false);
    }

    /// <summary>丢弃当前布局草稿，恢复已提交的位置。</summary>
    private void CancelEdit() => SetEditing(false);

    /// <summary>同步操作窗口、顶部提示与所有调整覆盖框。</summary>
    private void SetEditing(bool editing)
    {
        _chatResize.FinishResize();
        _chatResize.Visible = !editing;
        _editing = editing;
        _dragging = null;
        _hudEditWindow.Visible = editing;
        _hudEditHint.Visible = editing;
        if (editing) _hudEditWindow.FitToViewport();
        foreach (var handle in _handles.Values)
        {
            handle.Dragging = false;
            handle.Visible = editing;
        }
        if (!editing) _draftPositions.Clear();
        ApplyLayout();
    }

    /// <summary>处理调整覆盖框的按下、移动和松开事件。</summary>
    private void HandleDrag(string key, InputEvent input)
    {
        if (!_editing) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            _dragging = key;
            _handles[key].Dragging = true;
            var pointer = _handles[key].GetGlobalTransform() * button.Position;
            _dragOffset = pointer - _movable[key].GlobalPosition;
            AcceptEvent();
        }
    }

    /// <summary>按鼠标位置更新被拖动元素的相对坐标。</summary>
    private void MoveDraggedElement(string key, Vector2 mousePosition)
    {
        var viewport = GetViewportRect().Size;
        var panel = _movable[key];
        var desired = mousePosition - GlobalPosition - _dragOffset;
        var bounded = new Vector2(
            Mathf.Clamp(desired.X, 0, Mathf.Max(0, viewport.X - panel.Size.X)),
            Mathf.Clamp(desired.Y, 0, Mathf.Max(0, viewport.Y - panel.Size.Y)));
        panel.Position = bounded;
        _draftPositions[key] = new HudPosition(bounded.X / viewport.X, bounded.Y / viewport.Y);
    }

    /// <summary>按内容收紧 HUD，默认底栏齐平并保留自定义相对坐标。</summary>
    private void ApplyLayout()
    {
        var viewport = GetViewportRect().Size;
        _lastViewport = viewport;
        foreach (var panel in _movable.Values)
            if (panel != _chat) panel.Size = panel.GetCombinedMinimumSize();
        foreach (var (key, panel) in _movable)
        {
            if (key == "chat")
            {
                var chatSize = _editing ? _draftChatSize : GameServices.Setting.GetChatSize();
                _chat.Size = new Vector2(chatSize.Width, chatSize.Height);
            }
            var position = _editing ? _draftPositions[key] : GameServices.Setting.GetHudPosition(key);
            var desired = new Vector2(position.X * viewport.X, position.Y * viewport.Y);
            var initial = _defaultPositions[key];
            if (position.X == initial.X && position.Y == initial.Y)
            {
                if (key == "chat") desired = new Vector2(16, viewport.Y - panel.Size.Y - 16);
                else if (key == "radar") desired = new Vector2(viewport.X - panel.Size.X - 16, 16);
                else if (key == "menu") desired = viewport - panel.Size - new Vector2(16, 16);
                else if (key.StartsWith("hotbar", StringComparison.Ordinal))
                {
                    var index = int.Parse(key[^1..]) - 1;
                    desired = new Vector2((viewport.X - panel.Size.X) / 2,
                        viewport.Y - 16 - panel.Size.Y - index * (panel.Size.Y + 4));
                }
            }
            panel.Position = new Vector2(
                Mathf.Clamp(desired.X, 0, Mathf.Max(0, viewport.X - panel.Size.X)),
                Mathf.Clamp(desired.Y, 0, Mathf.Max(0, viewport.Y - panel.Size.Y)));
        }
        if (_systemMenuOverlay.Visible) PositionSystemMenu();
    }

    /// <summary>三个可移动窗口独立切换，其他普通面板切换时保留这些窗口。</summary>
    private void TogglePanel(string title)
    {
        var target = _panels.Single(panel => panel.Name == title);
        foreach (var panel in _panels)
        {
            if (panel == target) panel.Visible = !panel.Visible;
            else if (!IsIndependentWindow(target) && !IsIndependentWindow(panel)) panel.Hide();
            if (panel.Visible && panel is ViewportPanelControl bounded) bounded.FitToViewport();
        }
        if (target.Visible) MoveChild(target, GetChildCount() - 1);
    }

    /// <summary>角色、背包与大地图拥有独立显隐，不参与普通面板互斥。</summary>
    private static bool IsIndependentWindow(Control panel) => panel.Name == "角色" || panel.Name == "背包" || panel.Name == "大地图";

    /// <summary>从会话缓存刷新角色数值和地图位置。</summary>
    private void RefreshData()
    {
        var state = GameServices.Session.Role;
        _portraitName.Text = state.Name;
        _avatar.Level = state.Level;
        _avatar.Portrait = AvatarControl.LoadPortrait(state.Gender, state.HeadId);
        _avatar.QueueRedraw();
        _currentHealth = state.Health;
        _maxHealth = state.MaxHealth;
        var currentHealthPercent = HealthPercent(state.Health, state.MaxHealth);
        if (_healthPreviewPercent is null)
        {
            _healthPercentInput.SetValueNoSignal(currentHealthPercent);
            _healthPercentText.Text = $"{currentHealthPercent:0}%";
        }
        UpdateHealthBar(_healthPreviewPercent ?? currentHealthPercent);
        _manaBar.Value = Mathf.Clamp((float)state.Mana / Mathf.Max(1, state.MaxMana), 0, 1) * 100;
        _manaValue.Text = $"{state.Mana}/{state.MaxMana}";
        _characterText.Text = $"角色 ID：{state.RoleId}\n等级：{state.Level}\n生命：{state.Health}/{state.MaxHealth}\n魔力：{state.Mana}/{state.MaxMana}\n经验：{state.Experience}/{state.NextExperience}";
        _equipment.Refresh();
        var mapConfig = GameServices.World.MapConfig;
        _mapText.Text = $"地图：{mapConfig.Id}  {mapConfig.Width}×{mapConfig.Height}\n位置：{state.Position.X:0}, {state.Position.Y:0}";
        _miniMap.RolePosition = state.Position;
        _largeMap.RolePosition = state.Position;
        _miniMap.QueueRedraw();
        _largeMap.QueueRedraw();
    }

    /// <summary>将角色生命值换算为百分比并限制在状态条范围内。</summary>
    private static double HealthPercent(int health, int maxHealth) =>
        Mathf.Clamp((double)health / Math.Max(1, maxHealth) * 100, 0, 100);

    /// <summary>更新当前 HUD 血条长度与危险色阶。</summary>
    private void UpdateHealthBar(double percent)
    {
        _healthBar.Value = percent;
        _healthBar.AddThemeStyleboxOverride("fill", BarFill(HealthColor((float)percent / 100)));
        var visibleHealth = _healthPreviewPercent is null ? _currentHealth :
            (int)Math.Round(percent / 100 * _maxHealth, MidpointRounding.AwayFromZero);
        _healthValue.Text = $"{visibleHealth}/{_maxHealth}";
    }

    /// <summary>创建与 HUD 配色一致的紧凑状态条。</summary>
    private static ProgressBar CreateStatusBar(string name, Color color)
    {
        var bar = new ProgressBar { Name = name, CustomMinimumSize = new Vector2(140, 15),
            ShowPercentage = false, MinValue = 0, MaxValue = 100, MouseFilter = MouseFilterEnum.Ignore };
        var background = new StyleBoxFlat { BgColor = new Color("182630"), BorderColor = new Color("7899a6") };
        background.SetBorderWidthAll(1);
        background.SetCornerRadiusAll(5);
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", BarFill(color));
        return bar;
    }

    /// <summary>在状态条中央显示实际生命或魔力数值。</summary>
    private static Label CreateBarValue(string name, ProgressBar bar)
    {
        var label = Text("", 10);
        label.Name = name;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        bar.AddChild(label);
        return label;
    }

    /// <summary>生成生命与魔力条共用的填充样式。</summary>
    private static StyleBoxFlat BarFill(Color color)
    {
        var fill = new StyleBoxFlat { BgColor = color };
        fill.SetCornerRadiusAll(4);
        return fill;
    }

    /// <summary>按生命比例选取正常、警戒和危险颜色。</summary>
    internal static Color HealthColor(float ratio) => ratio <= 0.25f
        ? new Color("c94d4d") : ratio <= 0.5f ? new Color("d89137") : new Color("3ca66d");

    /// <summary>在界面上保留服务器或传输失败原因。</summary>
    public void ShowNetworkError(string error) => _networkError.Text = error;

    /// <summary>在返回页面时立即解除监听，不等待 QueueFree 执行。</summary>
    public void ReleaseSubscriptions()
    {
        var restoreDisplayPreview = _restoreDisplayPreview;
        _restoreDisplayPreview = null;
        restoreDisplayPreview?.Invoke();
        var restoreSoundPreview = _restoreSoundPreview;
        _restoreSoundPreview = null;
        restoreSoundPreview?.Invoke();
        _chatResize?.FinishResize();
        _chat?.ReleaseSubscription();
        _bag?.ReleaseSubscription();
        foreach (var handle in _handles.Values)
        {
            handle.Dragging = false;
            handle.Hide();
        }
        _hudEditWindow?.Hide();
        _hudEditHint?.Hide();
        _editing = false;
        _dragging = null;
        _draftPositions.Clear();
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    /// <summary>创建统一尺寸的普通文本。</summary>
    private static Label Text(string content, int fontSize)
    {
        var label = new Label { Text = content, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
