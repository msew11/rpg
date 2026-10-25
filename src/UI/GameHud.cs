using System;
using System.Collections.Generic;
using Godot;
using Rpg.Services;
using Rpg.Net.Message;
using Rpg.Settings.Data;

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
    /// <summary>头像区域中的角色状态文字。</summary>
    private Label _portraitText = null!;
    /// <summary>角色面板中的详细状态文字。</summary>
    private Label _characterText = null!;
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
    /// <summary>包含四项系统操作的上拉面板。</summary>
    private PanelContainer _systemMenu = null!;
    /// <summary>当前是否允许拖动 HUD。</summary>
    private bool _editing;
    /// <summary>调整期间独立于设置服务的坐标草稿。</summary>
    private Dictionary<string, HudPosition> _draftPositions = new();
    /// <summary>调整期间独立保存的聊天尺寸草稿。</summary>
    private DataHudSize _draftChatSize = new();
    /// <summary>当前抓取是否用于聊天框右上角缩放。</summary>
    private bool _resizingChat;
    /// <summary>缩放时固定的聊天框左下角坐标。</summary>
    private Vector2 _resizeAnchor;
    /// <summary>正在拖动的 HUD 元素键。</summary>
    private string? _dragging;
    /// <summary>鼠标与拖动左上角或缩放右上角参考点的距离。</summary>
    private Vector2 _dragOffset;
    /// <summary>上次布局计算使用的视口尺寸。</summary>
    private Vector2 _lastViewport;
    /// <summary>延迟加载素材目录的独立道具图鉴。</summary>
    private ItemCatalogControl _itemCatalog = null!;
    /// <summary>读取最新角色缓存的只读背包面板。</summary>
    private BagControl _bag = null!;
    /// <summary>独立于弹出面板显隐的聊天输入与 GM 反馈。</summary>
    private ChatControl _chat = null!;

    /// <summary>需要返回角色选择界面时通知主控制器。</summary>
    public event Action? ChangeRoleRequested;
    /// <summary>需要返回登录页面时通知主控制器。</summary>
    public event Action? LoginRequested;
    /// <summary>需要关闭客户端时通知主控制器。</summary>
    public event Action? QuitRequested;

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
        BuildSystemMenu();
        BuildHudEditWindow();
        ApplyLayout();
        RefreshData();
        _networkError = Text("", 14);
        _networkError.Name = "NetworkError";
        _networkError.Position = new Vector2(16, 140);
        _networkError.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_networkError);
        _subscriptions.Add(GameServices.EventBus.Subscribe<MoveRt>(OnMoved));
        _subscriptions.Add(GameServices.EventBus.Subscribe<RoleStateNotice>(OnRoleNotice));
        _subscriptions.Add(GameServices.EventBus.Subscribe<BagChangedNotice>(OnBagNotice));
    }

    /// <summary>退出界面时丢弃未确认的调整并释放会话订阅。</summary>
    public override void _ExitTree() => ReleaseSubscriptions();

    /// <summary>窗口或内容尺寸变化后重新收紧并排列 HUD。</summary>
    public override void _Process(double delta)
    {
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
        if (_systemMenuOverlay.Visible && input is InputEventKey { Keycode: Key.Escape, Pressed: true })
        {
            CloseSystemMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_editing && input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false })
        {
            if (_dragging is not null) _handles[_dragging].Dragging = false;
            _dragging = null;
            _resizingChat = false;
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
            _resizingChat = false;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>窗口失焦结束抓取，保留尚未确认的布局草稿。</summary>
    public override void _Notification(int what)
    {
        if (what != NotificationApplicationFocusOut || _dragging is null) return;
        _handles[_dragging].Dragging = false;
        _dragging = null;
        _resizingChat = false;
    }

    /// <summary>创建头像、小地图、三条九格技能栏和系统菜单。</summary>
    private void BuildHud()
    {
        var portrait = Movable("portrait", "角色头像");
        var portraitRow = new HBoxContainer();
        portrait.AddChild(portraitRow);
        portraitRow.AddChild(new AvatarControl { CustomMinimumSize = new Vector2(64, 64) });
        _portraitText = Text("", 14);
        portraitRow.AddChild(_portraitText);

        var radarBox = Movable("radar", "小地图");
        _movable["radar"].Name = "Radar";
        _movable["radar"].CustomMinimumSize = new Vector2(170, 170);
        radarBox.AddChild(Text("小地图", 15));
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
            CloseSystemMenu();
        };
        _systemMenu = new PanelContainer { Name = "SystemMenu", CustomMinimumSize = new Vector2(180, 0), MouseFilter = MouseFilterEnum.Stop };
        _systemMenuOverlay.AddChild(_systemMenu);
        var entries = new VBoxContainer();
        _systemMenu.AddChild(entries);
        AddSystemMenuItem(entries, "系统设置", () => TogglePanel("系统设置"));
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
        var handle = new HudEditOverlay { Name = "HudEditOverlay", Title = title, TopLevel = true, Visible = false, Resizable = key == "chat" };
        handle.GuiInput += input => HandleDrag(key, input);
        panel.AddChild(handle);
        _movable[key] = panel;
        _handles[key] = handle;
    }

    /// <summary>创建角色、背包、地图、GM 与系统面板及设置页签。</summary>
    private void BuildPanels()
    {
        var character = CreatePanel("角色", new Vector2(300, 255));
        _characterText = Text("", 16);
        character.AddChild(_characterText);
        var changeRole = new Button { Text = "切换角色" };
        changeRole.Pressed += () => ChangeRoleRequested?.Invoke();
        character.AddChild(changeRole);

        _bag = new BagControl { Name = "背包", Visible = false };
        AddChild(_bag);
        _panels.Add(_bag);

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
            foreach (var panel in _panels) panel.Hide();
            _itemCatalog.Open();
        };
        gmActions.AddChild(openCatalog);

        var system = CreatePanel("系统设置", new Vector2(380, 390));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(345, 295) };
        system.AddChild(tabs);
        BuildDisplayTab(tabs);
        BuildSoundTab(tabs);
        BuildHudTab(tabs);
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

    /// <summary>动态解析应用用户数据根目录，在系统文件管理器中打开其内容。</summary>
    private void OpenUserDataDirectory()
    {
        var error = OS.ShellShowInFileManager(ProjectSettings.GlobalizePath("user://"), true);
        _networkError.Text = error == Error.Ok ? "" : $"无法打开用户数据目录：{error}";
    }

    /// <summary>创建标题居左、关闭按钮靠右的弹出面板内容列。</summary>
    private VBoxContainer CreatePanel(string title, Vector2 minimum)
    {
        var outer = new ViewportPanelControl { Name = title, Title = title, PreferredWidth = minimum.X, Visible = false };
        AddChild(outer);
        _panels.Add(outer);
        return outer.Content;
    }

    /// <summary>加入分辨率选择和应用操作。</summary>
    private void BuildDisplayTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "显示" };
        tabs.AddChild(page);
        page.AddChild(Text("窗口分辨率", 16));
        var options = new OptionButton();
        var sizes = new[] { new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(1920, 1080) };
        for (var i = 0; i < sizes.Length; i++) options.AddItem($"{sizes[i].X} × {sizes[i].Y}", i);
        var selected = Array.FindIndex(sizes, size => size.X == GameServices.Setting.Width && size.Y == GameServices.Setting.Height);
        options.Select(Math.Max(0, selected));
        page.AddChild(options);
        var apply = new Button { Text = "应用并保存" };
        apply.Pressed += () =>
        {
            var size = sizes[options.Selected];
            GameServices.Setting.SetResolution(size.X, size.Y);
            DisplayServer.WindowSetSize(size);
            ApplyLayout();
        };
        page.AddChild(apply);
    }

    /// <summary>加入主音量滑块与保存操作。</summary>
    private void BuildSoundTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "声音" };
        tabs.AddChild(page);
        page.AddChild(Text("主音量", 16));
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = GameServices.Setting.MasterVolume * 100,
            CustomMinimumSize = new Vector2(280, 28) };
        page.AddChild(slider);
        var save = new Button { Text = "应用并保存" };
        save.Pressed += () =>
        {
            GameServices.Setting.SetMasterVolume((float)slider.Value / 100);
            ApplyVolume();
        };
        page.AddChild(save);
    }

    /// <summary>加入技能栏显隐和 HUD 拖动调整入口。</summary>
    private void BuildHudTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "角色 HUD" };
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
        _editing = editing;
        _dragging = null;
        _resizingChat = false;
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
            _resizingChat = _handles[key].IsResizeCorner(button.Position);
            var pointer = _handles[key].GetGlobalTransform() * button.Position;
            if (_resizingChat)
            {
                _resizeAnchor = new Vector2(_chat.Position.X, _chat.GetRect().End.Y);
                _dragOffset = pointer - (_chat.GlobalPosition + new Vector2(_chat.Size.X, 0));
            }
            else _dragOffset = pointer - _movable[key].GlobalPosition;
            AcceptEvent();
        }
    }

    /// <summary>按鼠标位置更新被拖动元素的相对坐标。</summary>
    private void MoveDraggedElement(string key, Vector2 mousePosition)
    {
        if (_resizingChat)
        {
            ResizeChat(mousePosition);
            return;
        }
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

    /// <summary>固定左下角从右上角缩放聊天，只修改布局草稿。</summary>
    private void ResizeChat(Vector2 mousePosition)
    {
        var minimum = _chat.GetCombinedMinimumSize().Max(new Vector2(DataHudSize.MinimumWidth, DataHudSize.MinimumHeight));
        var desired = mousePosition - GlobalPosition - _dragOffset;
        var viewport = GetViewportRect().Size;
        var right = Mathf.Clamp(desired.X, _resizeAnchor.X + minimum.X, Mathf.Max(_resizeAnchor.X + minimum.X, viewport.X));
        var top = Mathf.Clamp(desired.Y, 0, Mathf.Max(0, _resizeAnchor.Y - minimum.Y));
        _chat.Size = new Vector2(right - _resizeAnchor.X, _resizeAnchor.Y - top);
        _chat.Position = new Vector2(_resizeAnchor.X, top);
        _draftChatSize = new DataHudSize { Width = _chat.Size.X, Height = _chat.Size.Y };
        _draftPositions["chat"] = new HudPosition(_chat.Position.X / viewport.X, top / viewport.Y);
    }

    /// <summary>打开指定面板并关闭其他面板。</summary>
    private void TogglePanel(string title)
    {
        foreach (var panel in _panels)
        {
            panel.Visible = panel.Name == title ? !panel.Visible : false;
            if (panel.Visible && panel is ViewportPanelControl bounded) bounded.FitToViewport();
        }
    }

    /// <summary>从会话缓存刷新角色数值和地图位置。</summary>
    private void RefreshData()
    {
        var state = GameServices.Session.Role;
        _portraitText.Text = $"● {state.Name}  Lv.{state.Level}\n生命 {state.Health}/{state.MaxHealth}\n魔力 {state.Mana}/{state.MaxMana}\n经验 {state.Experience}/{state.NextExperience}";
        _characterText.Text = $"角色 ID：{state.RoleId}\n等级：{state.Level}\n生命：{state.Health}/{state.MaxHealth}\n魔力：{state.Mana}/{state.MaxMana}\n经验：{state.Experience}/{state.NextExperience}";
        var mapConfig = GameServices.Config.Map.Value;
        _mapText.Text = $"地图：{mapConfig.Id}  {mapConfig.Width}×{mapConfig.Height}\n位置：{state.Position.X:0}, {state.Position.Y:0}";
        _miniMap.RolePosition = state.Position;
        _largeMap.RolePosition = state.Position;
        _miniMap.QueueRedraw();
        _largeMap.QueueRedraw();
    }

    /// <summary>读取移动响应在缓存阶段更新后的快照。</summary>
    private void OnMoved(MoveRt response)
    {
        if (response.Success && response.RoleId == GameServices.Session.RoleId) RefreshData();
    }

    /// <summary>读取角色推送在缓存阶段更新后的快照。</summary>
    private void OnRoleNotice(RoleStateNotice notice)
    {
        if (notice.State?.RoleId == GameServices.Session.RoleId) RefreshData();
    }

    /// <summary>在缓存阶段接纳背包后刷新已打开的格子。</summary>
    private void OnBagNotice(BagChangedNotice notice)
    {
        if (notice.RoleId == GameServices.Session.RoleId) _bag.Refresh();
    }

    /// <summary>在界面上保留服务器或传输失败原因。</summary>
    public void ShowNetworkError(string error) => _networkError.Text = error;

    /// <summary>在返回页面时立即解除监听，不等待 QueueFree 执行。</summary>
    public void ReleaseSubscriptions()
    {
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
        _resizingChat = false;
        _draftPositions.Clear();
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    /// <summary>把保存的主音量应用到 Godot 音频总线。</summary>
    private void ApplyVolume()
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0) AudioServer.SetBusVolumeLinear(bus, GameServices.Setting.MasterVolume);
    }

    /// <summary>创建统一尺寸的普通文本。</summary>
    private static Label Text(string content, int fontSize)
    {
        var label = new Label { Text = content, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
