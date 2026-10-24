using System;
using System.Collections.Generic;
using Godot;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Services;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.UI;

/// <summary>串联配置启动、模拟登录、选角会话、世界和 HUD。</summary>
public partial class MainController : Control
{
    /// <summary>本次启动拥有的常驻服务释放句柄。</summary>
    private IDisposable? _servicesLifetime;
    /// <summary>主控制器拥有的常驻消息订阅。</summary>
    private readonly List<IDisposable> _subscriptions = [];
    /// <summary>登录或选角页的请求状态与错误反馈。</summary>
    private Label? _feedback;
    /// <summary>响应到达前防止重复登录的按钮。</summary>
    private Button? _loginButton;
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
        try
        {
            _servicesLifetime = InitializeServices();
            ApplySystemSettings();
            GameServices.Net.Error += OnNetworkError;
            GameServices.Events.Error += OnNetworkError;
            _subscriptions.Add(GameServices.Events.Subscribe<LoginRt>(OnLoggedIn));
            _subscriptions.Add(GameServices.Events.Subscribe<SelectRoleRt>(OnSelectedRole));
            ShowLogin();
        }
        catch (Exception error)
        {
            ReleaseServices();
            GD.PushError(error.ToString());
            ShowMessage("启动失败", error.Message);
        }
    }

    /// <summary>始终推进网络，再处理角色世界的 Action 与表现。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (_servicesLifetime is null) return;
        GameServices.Net.Tick(delta);
        GameServices.World.Tick(delta);
        if (_actor is not null && GameServices.World.ActorState is { } state) _actor.Present(state);
        if (_camera is not null && GameServices.World.HasRole)
            _camera.Position = IsometricProjection.ToScreen(GameServices.World.RolePosition);
    }

    /// <summary>将未被 UI 消费的地图点击转为移动 Action。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (_servicesLifetime is null || _worldVisual is null || input is not InputEventMouseButton click) return;
        var projectedPosition = GetViewport().GetCanvasTransform().AffineInverse() * click.Position;
        if (GameServices.World.HandleInput(input, IsometricProjection.ToLogical(projectedPosition)))
            GetViewport().SetInputAsHandled();
    }

    /// <summary>关闭场景时释放角色、所有常驻监听及网络传输。</summary>
    public override void _ExitTree() => ReleaseServices();

    /// <summary>启动完整服务组，场景测试可覆盖整组初始化以隔离环境。</summary>
    protected virtual IDisposable InitializeServices() => GameServices.Initialize();

    /// <summary>启动失败与场景退出共用清理顺序，最终释放本次启动的服务句柄。</summary>
    private void ReleaseServices()
    {
        var lifetime = _servicesLifetime;
        _servicesLifetime = null;
        try
        {
            _hud?.ReleaseSubscriptions();
            if (lifetime is not null)
            {
                GameServices.Net.Error -= OnNetworkError;
                GameServices.Events.Error -= OnNetworkError;
            }
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
        finally { lifetime?.Dispose(); }
    }

    /// <summary>展示用户名和密码输入及失败反馈。</summary>
    private void ShowLogin()
    {
        ClearGame();
        GameServices.Session.Logout();
        var box = CenteredScreen("网络 RPG 客户端 · 模拟登录");
        box.AddChild(Label("演示账号：demo / demo", 15));
        var username = new LineEdit { PlaceholderText = "用户名", Text = "demo", CustomMinimumSize = new Vector2(280, 36) };
        var password = new LineEdit { PlaceholderText = "密码", Secret = true, Text = "demo" };
        box.AddChild(username);
        box.AddChild(password);
        _feedback = Label("", 14);
        box.AddChild(_feedback);
        var login = new Button { Text = "登录", CustomMinimumSize = new Vector2(180, 38) };
        _loginButton = login;
        login.Pressed += () =>
        {
            login.Disabled = true;
            _feedback!.Text = "正在连接模拟服务器…";
            GameServices.Session.Login(username.Text, password.Text);
        };
        box.AddChild(login);
    }

    /// <summary>读取登录缓存展示角色列表及可选错误反馈。</summary>
    private void ShowRoles(string error = "")
    {
        ClearGame();
        var box = CenteredScreen("选择角色");
        _feedback = Label(error, 14);
        box.AddChild(_feedback);
        foreach (var role in GameServices.Session.Roles)
        {
            var button = new Button
            {
                Text = $"{role.Name} · {(role.Gender == RoleGender.Male ? "男" : "女")} · Lv.{role.Level} · ID {role.RoleId}",
                CustomMinimumSize = new Vector2(320, 48)
            };
            button.Pressed += () => StartRole(role.RoleId);
            box.AddChild(button);
        }
        var back = new Button { Text = "返回登录" };
        back.Pressed += ShowLogin;
        box.AddChild(back);
    }

    /// <summary>由已有账号会话重置角色缓存并提交选角，成功响应前不创建世界。</summary>
    private void StartRole(long roleId)
    {
        if (!GameServices.Session.IsLoggedIn || GameServices.Session.RoleId != 0) return;
        GameServices.Session.SelectRole(roleId);
        if (_feedback is not null) _feedback.Text = "正在进入角色…";
        if (_screen is not null)
            foreach (var node in _screen.FindChildren("*", "Button", true, false)) ((Button)node).Disabled = true;
    }

    /// <summary>登录业务处理完成后切换页面，失败时保留登录界面。</summary>
    private void OnLoggedIn(LoginRt response)
    {
        if (_loginButton is null) return;
        _loginButton.Disabled = false;
        if (!response.Success) { _feedback!.Text = response.Error; return; }
        ShowRoles();
    }

    /// <summary>缓存初始化成功后创建世界；选角失败只重置角色数据并保留账号会话。</summary>
    private void OnSelectedRole(SelectRoleRt response)
    {
        if (!GameServices.Session.IsLoggedIn || response.RoleId != GameServices.Session.RoleId) return;
        if (!response.Success)
        {
            if (GameServices.Session.IsLoggedIn) ShowRoles(response.Error);
            else ShowLogin();
            return;
        }
        if (!GameServices.Session.HasRole) throw new InvalidOperationException("选角响应未完成角色缓存初始化");
        BuildRoleWorld();
    }

    /// <summary>为服务已经初始化的世界建立地图、角色、摄像机与 HUD 表现。</summary>
    private void BuildRoleWorld()
    {
        _screen?.QueueFree();
        _screen = null;
        _feedback = null;
        _loginButton = null;
        _worldVisual = new Node2D { Name = "WorldVisual" };
        AddChild(_worldVisual);
        _worldVisual.AddChild(new MapView { Name = "MapView" });
        _actor = new ActorView { Name = "ActorView", Position = IsometricProjection.ToScreen(GameServices.World.RolePosition) };
        _worldVisual.AddChild(_actor);
        _actor.Present(GameServices.World.ActorState!);
        _camera = new Camera2D { Name = "WorldCamera", Position = IsometricProjection.ToScreen(GameServices.World.RolePosition), Enabled = true };
        _worldVisual.AddChild(_camera);

        _hudLayer = new CanvasLayer { Name = "HudLayer", Layer = 10 };
        AddChild(_hudLayer);
        _hud = new GameHud { Name = "GameHud" };
        _hudLayer.AddChild(_hud);
        _hud.ChangeRoleRequested += ReturnToRoles;
        _hud.LoginRequested += ShowLogin;
        _hud.QuitRequested += QuitGame;
    }

    /// <summary>结束角色世界并返回选角，保留同一个账号会话。</summary>
    private void ReturnToRoles()
    {
        if (GameServices.Session.IsLoggedIn) ShowRoles();
        else ShowLogin();
    }

    /// <summary>释放当前世界与会话后正常关闭客户端。</summary>
    private void QuitGame()
    {
        ClearGame();
        GetTree().Quit();
    }

    /// <summary>显示网络及事件监听错误，选角阶段失败时恢复可操作的角色列表。</summary>
    private void OnNetworkError(Exception error)
    {
        if (!GameServices.World.HasWorld && GameServices.Session.IsLoggedIn) ShowRoles(error.Message);
        else if (_feedback is not null) _feedback.Text = error.Message;
        else _hud?.ShowNetworkError(error.Message);
        if (_loginButton is not null) _loginButton.Disabled = false;
    }

    /// <summary>清理旧界面、世界和角色数据，保留账号会话与登录摘要。</summary>
    private void ClearGame()
    {
        _hud?.ReleaseSubscriptions();
        if (_servicesLifetime is not null)
        {
            GameServices.World.LeaveRole();
            GameServices.Session.LeaveRole();
        }
        _screen?.QueueFree();
        _screen = null;
        _feedback = null;
        _loginButton = null;
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

    /// <summary>在启动失败时展示可读错误。</summary>
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
        var width = Math.Clamp(GameServices.Setting.Width, 800, 3840);
        var height = Math.Clamp(GameServices.Setting.Height, 600, 2160);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
        var bus = AudioServer.GetBusIndex("Master");
        if (bus >= 0) AudioServer.SetBusVolumeLinear(bus, Mathf.Clamp(GameServices.Setting.MasterVolume, 0, 1));
    }

    /// <summary>创建指定字号的文本控件。</summary>
    private static Label Label(string content, int size)
    {
        var label = new Label { Text = content };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
