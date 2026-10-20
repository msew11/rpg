using System;
using Godot;
using Rpg.Config;
using Rpg.Input;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.UI;

/// <summary>串联配置启动、模拟登录、选角会话、世界和 HUD。</summary>
public partial class MainController : Control
{
    /// <summary>启动时加载的组合式游戏配置。</summary>
    private DataGameConfig _config = null!;
    /// <summary>当前使用的登录和状态来源。</summary>
    private readonly IStateSource _source = new MockStateSource();
    /// <summary>本地系统与角色设置仓库。</summary>
    private RepoSettings _settings = null!;
    /// <summary>当前系统显示和声音设置。</summary>
    private DataSystemSetting _systemSetting = null!;
    /// <summary>选中角色的活动会话。</summary>
    private GameSession? _session;
    /// <summary>活动会话对应的 ECS 世界。</summary>
    private WorldManager? _world;
    /// <summary>当前地图的坐标与地块管理器。</summary>
    private MapManager? _map;
    /// <summary>独立于世界模块的输入转换器。</summary>
    private readonly InputSystem _inputSystem = new();
    /// <summary>登录或选角页面。</summary>
    private CanvasLayer? _screen;
    /// <summary>地图、角色和摄像机的表现根节点。</summary>
    private Node2D? _worldVisual;
    /// <summary>角色绘制节点。</summary>
    private ActorView? _actor;
    /// <summary>跟随角色的世界摄像机。</summary>
    private Camera2D? _camera;
    /// <summary>独立于 ECS 的游戏界面。</summary>
    private GameHud? _hud;
    /// <summary>隔离世界摄像机变换的 HUD 画布层。</summary>
    private CanvasLayer? _hudLayer;

    /// <summary>加载配置和设置，随后展示模拟登录界面。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _settings = new RepoSettings(ProjectSettings.GlobalizePath("user://settings"));
        _systemSetting = _settings.LoadSystem();
        ApplySystemSettings();
        try
        {
            _config = new RepoGameConfig().LoadGodot();
            ShowLogin();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            ShowMessage("配置加载失败", error.Message);
        }
    }

    /// <summary>推进会话模拟源和 Action 驱动的 ECS。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (_session is null || _world is null) return;
        _session.Advance(delta);
        _world.Tick(delta);
        if (_actor is not null && _world.HasRole) _actor.Position = IsometricProjection.ToScreen(_world.RolePosition);
        if (_camera is not null && _world.HasRole) _camera.Position = IsometricProjection.ToScreen(_world.RolePosition);
    }

    /// <summary>将未被 UI 消费的地图点击转为移动 Action。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (_session is null || _world is null || _worldVisual is null || input is not InputEventMouseButton click) return;
        var projectedPosition = GetViewport().GetCanvasTransform().AffineInverse() * click.Position;
        var logicalPosition = IsometricProjection.ToLogical(projectedPosition);
        var action = _inputSystem.TranslateWorldClick(input, _map!.Clamp(logicalPosition));
        if (action is null) return;
        _world.Enqueue(action);
        _session.SubmitMove(action.ActionId, action.Destination);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>关闭场景时释放会话订阅。</summary>
    public override void _ExitTree() => _session?.Dispose();

    /// <summary>展示用户名和密码输入及失败反馈。</summary>
    private void ShowLogin()
    {
        ClearGame();
        var box = CenteredScreen("网络 RPG 客户端 · 模拟登录");
        box.AddChild(Label("演示账号：demo / demo", 15));
        var username = new LineEdit { PlaceholderText = "用户名", Text = "demo", CustomMinimumSize = new Vector2(280, 36) };
        var password = new LineEdit { PlaceholderText = "密码", Secret = true, Text = "demo" };
        box.AddChild(username);
        box.AddChild(password);
        var error = Label("", 14);
        box.AddChild(error);
        var login = new Button { Text = "登录", CustomMinimumSize = new Vector2(180, 38) };
        login.Pressed += () =>
        {
            login.Disabled = true;
            error.Text = "正在连接模拟状态源…";
            GetTree().CreateTimer(0.25).Timeout += () =>
            {
                if (!IsInsideTree()) return;
                var result = _source.Login(username.Text, password.Text);
                login.Disabled = false;
                if (!result.Success) error.Text = result.Error;
                else ShowRoles(result);
            };
        };
        box.AddChild(login);
    }

    /// <summary>展示模拟源返回的角色列表。</summary>
    private void ShowRoles(DataLoginResult result)
    {
        ClearGame();
        var box = CenteredScreen("选择角色");
        foreach (var role in result.Roles)
        {
            var button = new Button
            {
                Text = $"{role.Name} · Lv.{role.Level} · ID {role.RoleId}",
                CustomMinimumSize = new Vector2(320, 48)
            };
            button.Pressed += () => StartRole(role.RoleId);
            box.AddChild(button);
        }
        var back = new Button { Text = "返回登录" };
        back.Pressed += ShowLogin;
        box.AddChild(back);
    }

    /// <summary>为选中角色建立独立会话并依次初始化世界和角色。</summary>
    private void StartRole(long roleId)
    {
        ClearGame();
        _session = _source.SelectRole(roleId);
        _world = new WorldManager();
        _map = new MapManager(_config.Map);
        _world.Enqueue(new InitializeWorldAction(_map));
        _world.Enqueue(new InitializeRoleAction(_session.Role));
        _world.Tick(0);
        _session.Cache.RoleChanged += OnCacheRoleChanged;

        _worldVisual = new Node2D { Name = "WorldVisual" };
        AddChild(_worldVisual);
        _worldVisual.AddChild(new MapView { Map = _map, Name = "MapView" });
        _actor = new ActorView { Name = "ActorView", Position = IsometricProjection.ToScreen(_world.RolePosition) };
        _worldVisual.AddChild(_actor);
        _camera = new Camera2D { Name = "WorldCamera", Position = IsometricProjection.ToScreen(_world.RolePosition), Enabled = true };
        _worldVisual.AddChild(_camera);

        var roleSetting = _settings.LoadRole(roleId);
        _hudLayer = new CanvasLayer { Name = "HudLayer", Layer = 10 };
        AddChild(_hudLayer);
        _hud = new GameHud { Name = "GameHud" };
        _hudLayer.AddChild(_hud);
        _hud.Initialize(_config, _session, _settings, _systemSetting, roleSetting);
        _hud.ChangeRoleRequested += () => ShowRoles(_source.Login("demo", "demo"));
    }

    /// <summary>将缓存中的权威状态变更转化为 ECS Action。</summary>
    private void OnCacheRoleChanged(DataRoleState state) => _world?.Enqueue(new SyncRoleAction(state));

    /// <summary>清理旧界面、世界及会话订阅。</summary>
    private void ClearGame()
    {
        if (_session is not null)
        {
            _session.Cache.RoleChanged -= OnCacheRoleChanged;
            _session.Dispose();
            _session = null;
        }
        _world = null;
        _map = null;
        _screen?.QueueFree();
        _screen = null;
        _hudLayer?.QueueFree();
        _hudLayer = null;
        _hud = null;
        _worldVisual?.QueueFree();
        _worldVisual = null;
        _actor = null;
        _camera = null;
    }

    /// <summary>创建居中的启动或选择角色页面。</summary>
    private VBoxContainer CenteredScreen(string title)
    {
        _screen = new CanvasLayer { Name = "ScreenLayer", Layer = 10 };
        AddChild(_screen);
        var background = new ColorRect { Color = new Color("13242d"), AnchorRight = 1, AnchorBottom = 1 };
        _screen.AddChild(background);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(390, 310) };
        center.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        box.AddChild(Label(title, 22));
        return box;
    }

    /// <summary>在配置加载失败时展示可读错误。</summary>
    private void ShowMessage(string title, string message)
    {
        var box = CenteredScreen(title);
        var label = Label(message, 15);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(label);
    }

    /// <summary>应用本地系统分辨率与音量。</summary>
    private void ApplySystemSettings()
    {
        var width = Math.Clamp(_systemSetting.Width, 800, 3840);
        var height = Math.Clamp(_systemSetting.Height, 600, 2160);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
        var bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0) AudioServer.SetBusVolumeLinear(bus, Mathf.Clamp(_systemSetting.MasterVolume, 0, 1));
    }

    /// <summary>创建指定字号的文本控件。</summary>
    private static Label Label(string content, int size)
    {
        var label = new Label { Text = content };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
