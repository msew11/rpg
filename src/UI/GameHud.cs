using System;
using System.Collections.Generic;
using Godot;
using Rpg.Config;
using Rpg.Session;
using Rpg.Settings;

namespace Rpg.UI;

/// <summary>管理独立于 ECS 世界的 HUD、系统面板与本地设置界面。</summary>
public partial class GameHud : Control
{
    /// <summary>可调整位置的 HUD 元素。</summary>
    private readonly Dictionary<string, PanelContainer> _movable = new();
    /// <summary>HUD 元素的拖动把手。</summary>
    private readonly Dictionary<string, Label> _handles = new();
    /// <summary>角色、背包、地图和系统弹出面板。</summary>
    private readonly List<Control> _panels = [];
    /// <summary>当前组合游戏配置。</summary>
    private DataGameConfig _config = null!;
    /// <summary>当前角色会话。</summary>
    private GameSession _session = null!;
    /// <summary>本地设置仓库。</summary>
    private RepoSettings _settings = null!;
    /// <summary>当前角色 HUD 设置。</summary>
    private DataRoleSetting _roleSetting = null!;
    /// <summary>当前系统显示及声音设置。</summary>
    private DataSystemSetting _systemSetting = null!;
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
    /// <summary>当前是否允许拖动 HUD。</summary>
    private bool _editing;
    /// <summary>正在拖动的 HUD 元素键。</summary>
    private string? _dragging;
    /// <summary>鼠标与被拖动元素左上角的距离。</summary>
    private Vector2 _dragOffset;
    /// <summary>上次布局计算使用的视口尺寸。</summary>
    private Vector2 _lastViewport;

    /// <summary>需要返回角色选择界面时通知主控制器。</summary>
    public event Action? ChangeRoleRequested;

    /// <summary>用选中会话、配置及设置创建完整游戏界面。</summary>
    public void Initialize(DataGameConfig config, GameSession session, RepoSettings settings,
        DataSystemSetting systemSetting, DataRoleSetting roleSetting)
    {
        _config = config;
        _session = session;
        _settings = settings;
        _systemSetting = systemSetting;
        _roleSetting = roleSetting;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        BuildHud();
        BuildPanels();
        ApplyLayout();
        RefreshData();
        _session.Cache.RoleChanged += OnRoleChanged;
    }

    /// <summary>释放会话缓存订阅。</summary>
    public override void _ExitTree()
    {
        if (_session is not null) _session.Cache.RoleChanged -= OnRoleChanged;
    }

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
        if (!_editing || _dragging is null) return;
        if (input is InputEventMouseMotion motion)
        {
            MoveDraggedElement(_dragging, motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dragging = null;
            SaveRole();
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
        _miniMap = new MiniMapControl { CustomMinimumSize = new Vector2(150, 145), Map = _config.Map };
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
            _movable[$"hotbar{bar}"].Visible = _roleSetting.VisibleHotbars[bar - 1];
        }

        var menu = Movable("menu", new Vector2(280, 65), "系统菜单");
        var menuRow = new HBoxContainer();
        menu.AddChild(menuRow);
        foreach (var title in new[] { "角色", "背包", "大地图", "系统" })
        {
            var button = new Button { Text = title, CustomMinimumSize = new Vector2(62, 34) };
            button.Pressed += () => TogglePanel(title);
            menuRow.AddChild(button);
        }
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

    /// <summary>创建四个基础面板与分类设置页签。</summary>
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
        _largeMap = new MiniMapControl { CustomMinimumSize = new Vector2(390, 340), Map = _config.Map };
        mapPanel.AddChild(_largeMap);

        var system = CreatePanel("系统", new Vector2(380, 390));
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(345, 295) };
        system.AddChild(tabs);
        BuildDisplayTab(tabs);
        BuildSoundTab(tabs);
        BuildHudTab(tabs);
    }

    /// <summary>创建带关闭按钮的弹出面板内容列。</summary>
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
        header.AddChild(Text(title, 20));
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
        var selected = Array.FindIndex(sizes, size => size.X == _systemSetting.Width && size.Y == _systemSetting.Height);
        options.Select(Math.Max(0, selected));
        page.AddChild(options);
        var apply = new Button { Text = "应用并保存" };
        apply.Pressed += () =>
        {
            var size = sizes[options.Selected];
            _systemSetting.Width = size.X;
            _systemSetting.Height = size.Y;
            DisplayServer.WindowSetSize(size);
            _settings.SaveSystem(_systemSetting);
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
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = _systemSetting.MasterVolume * 100,
            CustomMinimumSize = new Vector2(280, 28) };
        page.AddChild(slider);
        var save = new Button { Text = "应用并保存" };
        save.Pressed += () =>
        {
            _systemSetting.MasterVolume = (float)slider.Value / 100;
            ApplyVolume();
            _settings.SaveSystem(_systemSetting);
        };
        page.AddChild(save);
    }

    /// <summary>加入技能栏显隐和 HUD 拖动调整入口。</summary>
    private void BuildHudTab(TabContainer tabs)
    {
        var page = new VBoxContainer { Name = "角色 HUD" };
        tabs.AddChild(page);
        page.AddChild(Text("技能栏 1 始终显示", 14));
        _secondBar = new CheckBox { Text = "显示技能栏 2", ButtonPressed = _roleSetting.VisibleHotbars[1] };
        _thirdBar = new CheckBox { Text = "显示技能栏 3", ButtonPressed = _roleSetting.VisibleHotbars[2] };
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
            _roleSetting.HudPositions = DataRoleSetting.DefaultPositions();
            ApplyLayout();
            SaveRole();
        };
        page.AddChild(reset);
    }

    /// <summary>根据角色设置显示或隐藏指定技能栏。</summary>
    private void SetBarVisible(int index, bool visible)
    {
        _roleSetting.VisibleHotbars[index - 1] = visible;
        _movable[$"hotbar{index}"].Visible = visible;
        SaveRole();
    }

    /// <summary>切换 HUD 调整模式及拖动把手。</summary>
    private void ToggleEdit()
    {
        _editing = !_editing;
        _editButton.Text = _editing ? "完成 HUD 调整" : "进入 HUD 调整";
        foreach (var handle in _handles.Values) handle.Visible = _editing;
        if (!_editing) { _dragging = null; SaveRole(); }
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
        _roleSetting.HudPositions[key] = new DataHudPosition(bounded.X / viewport.X, bounded.Y / viewport.Y);
    }

    /// <summary>按保存的屏幕相对坐标排列并约束 HUD。</summary>
    private void ApplyLayout()
    {
        var viewport = GetViewportRect().Size;
        _lastViewport = viewport;
        foreach (var (key, panel) in _movable)
        {
            var position = _roleSetting.HudPositions[key];
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
        var state = _session.Cache.Role;
        var role = state.Role;
        _portraitText.Text = $"● {role.Name}  Lv.{role.Level}\n生命 {role.Health}/{role.MaxHealth}\n魔力 {role.Mana}/{role.MaxMana}\n经验 {role.Experience}/{role.NextExperience}";
        _characterText.Text = $"角色 ID：{role.RoleId}\n等级：{role.Level}\n生命：{role.Health}/{role.MaxHealth}\n魔力：{role.Mana}/{role.MaxMana}\n经验：{role.Experience}/{role.NextExperience}";
        _mapText.Text = $"地图：{_config.Map.Id}  {_config.Map.Width}×{_config.Map.Height}\n位置：{state.Position.X:0}, {state.Position.Y:0}";
        _miniMap.RolePosition = state.Position;
        _largeMap.RolePosition = state.Position;
        _miniMap.QueueRedraw();
        _largeMap.QueueRedraw();
    }

    /// <summary>响应缓存变化并更新世界外 UI。</summary>
    private void OnRoleChanged(DataRoleState state) => RefreshData();

    /// <summary>持久化当前角色的 HUD 配置。</summary>
    private void SaveRole() => _settings.SaveRole(_session.Role.RoleId, _roleSetting);

    /// <summary>把保存的主音量应用到 Godot 音频总线。</summary>
    public void ApplyVolume()
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0) AudioServer.SetBusVolumeLinear(bus, _systemSetting.MasterVolume);
    }

    /// <summary>创建统一尺寸的普通文本。</summary>
    private static Label Text(string content, int fontSize)
    {
        var label = new Label { Text = content, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
