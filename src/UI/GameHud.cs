using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Rpg.Services;
using Rpg.Net.Message;

namespace Rpg.UI;

/// <summary>管理独立于 ECS 世界的 HUD、系统面板与本地设置界面。</summary>
public partial class GameHud : Control
{
    /// <summary>可调整位置的 HUD 元素。</summary>
    private readonly Dictionary<string, PanelContainer> _movable = new();
    /// <summary>HUD 元素的拖动把手。</summary>
    private readonly Dictionary<string, Label> _handles = new();
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
    /// <summary>HUD 调整模式的切换按钮。</summary>
    private Button _editButton = null!;
    /// <summary>用于定位上拉菜单和恢复键盘焦点的系统按钮。</summary>
    private Button _systemButton = null!;
    /// <summary>菜单展开时消费外部点击的透明遮罩。</summary>
    private Control _systemMenuOverlay = null!;
    /// <summary>包含四项系统操作的上拉面板。</summary>
    private PanelContainer _systemMenu = null!;
    /// <summary>当前是否允许拖动 HUD。</summary>
    private bool _editing;
    /// <summary>正在拖动的 HUD 元素键。</summary>
    private string? _dragging;
    /// <summary>鼠标与被拖动元素左上角的距离。</summary>
    private Vector2 _dragOffset;
    /// <summary>上次布局计算使用的视口尺寸。</summary>
    private Vector2 _lastViewport;
    /// <summary>延迟加载素材目录的独立道具图鉴。</summary>
    private ItemCatalogControl _itemCatalog = null!;

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
        BuildSystemMenu();
        ApplyLayout();
        RefreshData();
        _networkError = Text("", 14);
        _networkError.Name = "NetworkError";
        _networkError.Position = new Vector2(16, 140);
        _networkError.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_networkError);
        _subscriptions.Add(GameServices.EventBus.Subscribe<MoveRt>(OnMoved));
        _subscriptions.Add(GameServices.EventBus.Subscribe<RoleStateNotice>(OnRoleNotice));
    }

    /// <summary>退出界面时保存未提交的拖动位置并释放会话订阅。</summary>
    public override void _ExitTree() => ReleaseSubscriptions();

    /// <summary>窗口变化后重新按相对坐标布局。</summary>
    public override void _Process(double delta)
    {
        var size = GetViewportRect().Size;
        if (size == _lastViewport) return;
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
        if (!_editing || _dragging is null) return;
        if (input is InputEventMouseMotion motion)
        {
            MoveDraggedElement(_dragging, motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dragging = null;
            GameServices.Setting.SaveCurrentRole();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>创建头像、小地图、三条九格技能栏和系统菜单。</summary>
    private void BuildHud()
    {
        var portrait = Movable("portrait", new Vector2(220, 130), "角色头像");
        var portraitRow = new HBoxContainer();
        portrait.AddChild(portraitRow);
        portraitRow.AddChild(new AvatarControl { CustomMinimumSize = new Vector2(64, 64) });
        _portraitText = Text("", 14);
        portraitRow.AddChild(_portraitText);

        var radarPanel = new PanelContainer
        {
            Name = "Radar",
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -186,
            OffsetRight = -16,
            OffsetTop = 16,
            OffsetBottom = 186,
            CustomMinimumSize = new Vector2(170, 170)
        };
        AddChild(radarPanel);
        var radarBox = new VBoxContainer();
        radarPanel.AddChild(radarBox);
        radarBox.AddChild(Text("小地图", 15));
        _miniMap = new MiniMapControl { CustomMinimumSize = new Vector2(150, 145) };
        radarBox.AddChild(_miniMap);

        for (var bar = 1; bar <= 3; bar++)
        {
            var panel = Movable($"hotbar{bar}", new Vector2(432, 66), $"技能栏 {bar}");
            var row = new HBoxContainer();
            panel.AddChild(row);
            for (var slot = 1; slot <= 9; slot++)
            {
                var cell = new PanelContainer { CustomMinimumSize = new Vector2(44, 36) };
                var label = Text(slot.ToString(), 13);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                cell.AddChild(label);
                row.AddChild(cell);
            }
            _movable[$"hotbar{bar}"].Visible = GameServices.Setting.IsHotbarVisible(bar);
        }

        var menu = Movable("menu", new Vector2(355, 65), "系统菜单");
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
        _systemMenu.Size = _systemMenu.GetCombinedMinimumSize();
        var button = _systemButton.GetGlobalRect();
        var desired = new Vector2(button.End.X - _systemMenu.Size.X, button.Position.Y - _systemMenu.Size.Y - 4) - GlobalPosition;
        var viewport = GetViewportRect().Size;
        _systemMenu.Position = new Vector2(
            Mathf.Clamp(desired.X, 0, Mathf.Max(0, viewport.X - _systemMenu.Size.X)),
            Mathf.Clamp(desired.Y, 0, Mathf.Max(0, viewport.Y - _systemMenu.Size.Y)));
        _systemMenuOverlay.Show();
        _systemMenu.GetChild(0).GetChild<Button>(0).GrabFocus();
    }

    /// <summary>收起系统菜单并将键盘焦点还给入口按钮。</summary>
    private void CloseSystemMenu()
    {
        if (!_systemMenuOverlay.Visible) return;
        _systemMenuOverlay.Hide();
        _systemButton.GrabFocus();
    }

    /// <summary>创建一个可在 HUD 编辑模式拖动的元素。</summary>
    private VBoxContainer Movable(string key, Vector2 minimum, string title)
    {
        var panel = new PanelContainer { Name = key, CustomMinimumSize = minimum };
        AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        var handle = Text(title + "  拖动", 12);
        handle.Visible = false;
        handle.MouseFilter = MouseFilterEnum.Stop;
        handle.GuiInput += input => HandleDrag(key, input);
        box.AddChild(handle);
        _movable[key] = panel;
        _handles[key] = handle;
        return box;
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

        var inventory = CreatePanel("背包", new Vector2(280, 190));
        inventory.AddChild(Text("背包为空", 16));

        var mapPanel = CreatePanel("大地图", new Vector2(440, 440));
        _mapText = Text("", 14);
        mapPanel.AddChild(_mapText);
        _largeMap = new MiniMapControl { CustomMinimumSize = new Vector2(390, 340) };
        mapPanel.AddChild(_largeMap);

        var gm = CreatePanel("GM", new Vector2(590, 255));
        AddSettingLocation(gm, "系统设置文件", GameServices.Setting.SystemPath);
        AddSettingLocation(gm, "当前角色设置文件", GameServices.Setting.RolePath);
        _itemCatalog = new ItemCatalogControl { Name = "ItemCatalog", Visible = false };
        AddChild(_itemCatalog);
        _panels.Add(_itemCatalog);
        var openCatalog = new Button { Name = "OpenItemCatalog", Text = "查看道具图鉴" };
        openCatalog.Pressed += () =>
        {
            foreach (var panel in _panels) panel.Hide();
            _itemCatalog.Open();
        };
        gm.AddChild(openCatalog);

        var system = CreatePanel("系统设置", new Vector2(380, 390));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(345, 295) };
        system.AddChild(tabs);
        BuildDisplayTab(tabs);
        BuildSoundTab(tabs);
        BuildHudTab(tabs);
    }

    /// <summary>显示设置文件位置，并提供文件管理器定位按钮。</summary>
    private void AddSettingLocation(VBoxContainer panel, string title, string path)
    {
        panel.AddChild(Text(title, 15));
        panel.AddChild(new LineEdit
        {
            Text = path,
            Editable = false,
            CustomMinimumSize = new Vector2(540, 34),
            FocusMode = FocusModeEnum.None
        });
        var open = new Button { Text = $"打开{title}位置" };
        open.Pressed += () =>
        {
            var exists = File.Exists(path);
            var target = exists ? path : Path.GetDirectoryName(path)!;
            if (!exists) Directory.CreateDirectory(target);
            OS.ShellShowInFileManager(target, !exists);
        };
        panel.AddChild(open);
    }

    /// <summary>创建标题居左、关闭按钮靠右的弹出面板内容列。</summary>
    private VBoxContainer CreatePanel(string title, Vector2 minimum)
    {
        var outer = new PanelContainer { CustomMinimumSize = minimum, Visible = false, MouseFilter = MouseFilterEnum.Stop };
        outer.AnchorLeft = 0.5f;
        outer.AnchorTop = 0.5f;
        outer.AnchorRight = 0.5f;
        outer.AnchorBottom = 0.5f;
        outer.Position = -minimum / 2;
        AddChild(outer);
        _panels.Add(outer);
        outer.Name = title;
        var box = new VBoxContainer();
        outer.AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        var heading = Text(title, 20);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(heading);
        var close = new Button { Text = "关闭" };
        close.Pressed += () => outer.Visible = false;
        header.AddChild(close);
        return box;
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
        _editButton = new Button { Text = "进入 HUD 调整" };
        _editButton.Pressed += ToggleEdit;
        page.AddChild(_editButton);
        var reset = new Button { Text = "恢复默认布局" };
        reset.Pressed += () =>
        {
            GameServices.Setting.ResetHudPositions();
            ApplyLayout();
        };
        page.AddChild(reset);
    }

    /// <summary>根据角色设置显示或隐藏指定技能栏。</summary>
    private void SetBarVisible(int index, bool visible)
    {
        GameServices.Setting.SetHotbarVisible(index, visible);
        _movable[$"hotbar{index}"].Visible = visible;
    }

    /// <summary>切换 HUD 调整模式及拖动把手。</summary>
    private void ToggleEdit()
    {
        _editing = !_editing;
        _editButton.Text = _editing ? "完成 HUD 调整" : "进入 HUD 调整";
        foreach (var handle in _handles.Values) handle.Visible = _editing;
        if (!_editing) { _dragging = null; GameServices.Setting.SaveCurrentRole(); }
    }

    /// <summary>处理拖动把手的按下、移动和松开事件。</summary>
    private void HandleDrag(string key, InputEvent input)
    {
        if (!_editing) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            _dragging = key;
            _dragOffset = button.Position - _movable[key].Position;
            AcceptEvent();
        }
    }

    /// <summary>按鼠标位置更新被拖动元素的相对坐标。</summary>
    private void MoveDraggedElement(string key, Vector2 mousePosition)
    {
        var viewport = GetViewportRect().Size;
        var panel = _movable[key];
        var desired = mousePosition - _dragOffset;
        var bounded = new Vector2(
            Mathf.Clamp(desired.X, 0, Mathf.Max(0, viewport.X - panel.Size.X)),
            Mathf.Clamp(desired.Y, 0, Mathf.Max(0, viewport.Y - panel.Size.Y)));
        panel.Position = bounded;
        GameServices.Setting.SetHudPosition(key, bounded.X / viewport.X, bounded.Y / viewport.Y);
    }

    /// <summary>按保存的屏幕相对坐标排列并约束 HUD。</summary>
    private void ApplyLayout()
    {
        CloseSystemMenu();
        var viewport = GetViewportRect().Size;
        _lastViewport = viewport;
        foreach (var (key, panel) in _movable)
        {
            var position = GameServices.Setting.GetHudPosition(key);
            panel.Position = new Vector2(
                Mathf.Clamp(position.X * viewport.X, 0, Mathf.Max(0, viewport.X - panel.Size.X)),
                Mathf.Clamp(position.Y * viewport.Y, 0, Mathf.Max(0, viewport.Y - panel.Size.Y)));
        }
    }

    /// <summary>打开指定面板并关闭其他面板。</summary>
    private void TogglePanel(string title)
    {
        foreach (var panel in _panels)
            panel.Visible = panel.Name == title ? !panel.Visible : false;
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

    /// <summary>在界面上保留服务器或传输失败原因。</summary>
    public void ShowNetworkError(string error) => _networkError.Text = error;

    /// <summary>在返回页面时立即解除监听，不等待 QueueFree 执行。</summary>
    public void ReleaseSubscriptions()
    {
        if (_dragging is not null && GameServices.IsInitialized)
        {
            GameServices.Setting.SaveCurrentRole();
            _dragging = null;
        }
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
