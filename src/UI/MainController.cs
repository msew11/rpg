using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
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
    /// <summary>独立 V2 场景接管系统关闭，以便保存失败时保留页面。</summary>
    private bool _quitPolicyManaged;
    /// <summary>场景结束时恢复原有 SceneTree 关闭策略。</summary>
    private bool _previousAutoAcceptQuit;
    /// <summary>主控制器拥有的常驻消息订阅。</summary>
    private readonly List<IDisposable> _subscriptions = [];
    /// <summary>登录或选角页的请求状态与错误反馈。</summary>
    private Label? _feedback;
    /// <summary>响应到达前防止重复登录的按钮。</summary>
    private Button? _loginButton;
    /// <summary>登录或选角页面。</summary>
    private CanvasLayer? _screen;
    /// <summary>当前创建表单，失败时保留用户草稿。</summary>
    private RoleCreateControl? _createPage;
    /// <summary>角色列表当前是否等待管理请求响应。</summary>
    private bool _roleRequestPending;
    /// <summary>地图、角色和摄像机的表现根节点。</summary>
    private Node2D? _worldVisual;
    /// <summary>只读地面表现与命中查询，不复制地图对象。</summary>
    private GroundItemsView? _groundItems;
    /// <summary>拥有地面名称和信息框的 UI 层。</summary>
    private GroundItemOverlayControl? _groundOverlay;
    /// <summary>角色绘制节点。</summary>
    private ActorView? _actor;
    /// <summary>跟随角色的世界摄像机。</summary>
    private Camera2D? _camera;
    /// <summary>独立于 ECS 的游戏界面。</summary>
    private GameHud? _hud;
    /// <summary>主场景持有的页面与地图背景音乐播放器。</summary>
    private BgmPlayer? _bgm;
    /// <summary>隔离世界摄像机变换的 HUD 画布层。</summary>
    private CanvasLayer? _hudLayer;

    /// <summary>设置普通鼠标，加载配置和设置，随后展示模拟登录界面。</summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        try
        {
            AddChild(new PointerCursor { Name = "PointerCursor" });
            _servicesLifetime = InitializeServices();
            AddChild(new UiSoundPlayer { Name = "UiSound" });
            _bgm = new BgmPlayer { Name = "Bgm" };
            AddChild(_bgm);
            ApplySystemSettings();
            GameServices.Net.Error += OnNetworkError;
            GameServices.EventBus.Error += OnNetworkError;
            if (GameServices.Net.MockServer is not null)
            {
                _previousAutoAcceptQuit = GetTree().AutoAcceptQuit;
                _quitPolicyManaged = true;
                GetTree().AutoAcceptQuit = false;
                _subscriptions.Add(GameServices.EventBus.Subscribe<ProtocolV2.LoginRt>(_ => { if (_loginButton is not null) ShowRoles(); }));
                _subscriptions.Add(GameServices.EventBus.Subscribe<ProtocolV2.CreateRoleRt>(_ => { if (_roleRequestPending) ShowRoles(); }));
                _subscriptions.Add(GameServices.EventBus.Subscribe<ProtocolV2.DeleteRoleRt>(_ => { if (_roleRequestPending) ShowRoles(); }));
                _subscriptions.Add(GameServices.EventBus.Subscribe<ProtocolV2.EnterWorldRt>(OnEnteredWorld));
                _subscriptions.Add(GameServices.EventBus.Subscribe<ProtocolV2.ErrorRt>(reply => OnNetworkError(new InvalidOperationException(reply.Message))));
            }
            else _subscriptions.Add(GameServices.EventBus.Subscribe<LoginRt>(OnLoggedIn));
            _subscriptions.Add(GameServices.EventBus.Subscribe<SelectRoleRt>(OnSelectedRole));
            _subscriptions.Add(GameServices.EventBus.Subscribe<CreateRoleRt>(OnCreatedRole));
            _subscriptions.Add(GameServices.EventBus.Subscribe<DeleteRoleRt>(OnDeletedRole));
            _subscriptions.Add(GameServices.EventBus.Subscribe<MoveRt>(OnMoved));
            _subscriptions.Add(GameServices.EventBus.Subscribe<RoleStateNotice>(OnRoleNotice));
            _subscriptions.Add(GameServices.EventBus.Subscribe<RoleProfileNotice>(OnProfileNotice));
            _subscriptions.Add(GameServices.EventBus.Subscribe<RolePositionNotice>(OnPositionNotice));
            _subscriptions.Add(GameServices.EventBus.Subscribe<MapAoiNotice>(OnMapNotice));
            _subscriptions.Add(GameServices.EventBus.Subscribe<PickupItemRt>(OnPickedUp));
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

    /// <summary>将未被 UI 消费的地图点击转为移动 Action，并在点击世界位置显示光圈。</summary>
    public override void _UnhandledInput(InputEvent input)
    {
        if (_servicesLifetime is null || _worldVisual is null || GameServices.Session.IsWorldReadOnly || input is not InputEventMouseButton click) return;
        if (click is { ButtonIndex: MouseButton.Left, Pressed: true } && _groundOverlay?.ItemAt(click.Position) is { } objectId)
        {
            RequestPickup(objectId);
            GetViewport().SetInputAsHandled();
            return;
        }
        var projectedPosition = GetViewport().GetCanvasTransform().AffineInverse() * click.Position;
        if (GameServices.World.HandleInput(input, IsometricProjection.ToLogical(projectedPosition)))
        {
            _worldVisual.AddChild(new GroundClickView { Position = projectedPosition });
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>图标或 UI 信息框拾取均使用同一世界服务入口。</summary>
    private void RequestPickup(Guid objectId)
    {
        if (GameServices.Session.IsWorldReadOnly) return;
        try { GameServices.World.PickupItem(objectId); }
        catch (Exception error) { _hud?.ShowNetworkError(error.Message); }
    }

    /// <summary>关闭场景时恢复鼠标并释放角色、所有常驻监听及网络传输。</summary>
    public override void _ExitTree()
    {
        Input.SetCustomMouseCursor(null);
        ReleaseServices();
    }

    /// <summary>系统关闭与菜单退出共用保存失败可重试的离开流程。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && _quitPolicyManaged && _servicesLifetime is not null) QuitGame();
    }

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
                GameServices.EventBus.Error -= OnNetworkError;
            }
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
        finally
        {
            if (_quitPolicyManaged) { GetTree().AutoAcceptQuit = _previousAutoAcceptQuit; _quitPolicyManaged = false; }
            lifetime?.Dispose();
        }
    }

    /// <summary>展示用户名和密码输入及失败反馈。</summary>
    private void ShowLogin()
    {
        if (!TryFlushBeforeLeavingRole(includeAccount: true)) return;
        ClearGame();
        GameServices.Session.Logout();
        _bgm?.PlayTrack("M001");
        var box = CenteredScreen("网络 RPG 客户端 · 模拟登录");
        box.AddChild(Label("演示账号：demo / demo", 15));
        var username = new LineEdit { Name = "Username", PlaceholderText = "用户名", Text = "demo", CustomMinimumSize = new Vector2(280, 36) };
        var password = new LineEdit { Name = "Password", PlaceholderText = "密码", Secret = true, Text = "demo" };
        // 让两个文本框之间的窄缝延续 I 形鼠标，保持原有间距和输入行为。
        var fields = new VBoxContainer { Name = "LoginFields", MouseDefaultCursorShape = Control.CursorShape.Ibeam };
        fields.AddThemeConstantOverride("separation", box.GetThemeConstant("separation"));
        box.AddChild(fields);
        fields.AddChild(username);
        fields.AddChild(password);
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
        if (!TryFlushBeforeLeavingRole()) return;
        ClearGame();
        _bgm?.PlayTrack("M001");
        _screen = new CanvasLayer { Name = "ScreenLayer", Layer = 10 };
        AddChild(_screen);
        var page = new RoleSelectionControl { Name = "RoleSelectionPage", CanEnterWorld = true };
        page.EnterRequested += StartRole;
        page.CreateRequested += ShowCreateRole;
        page.DeleteRequested += ConfirmDeleteRole;
        page.LoginRequested += ShowLogin;
        _screen.AddChild(page);
        _feedback = page.Feedback;
        _feedback.Text = error;
    }

    /// <summary>建立右侧外观选项与中央预览，命名确认后提交创建。</summary>
    private void ShowCreateRole()
    {
        if (_roleRequestPending) return;
        ClearGame();
        _screen = new CanvasLayer { Name = "ScreenLayer", Layer = 10 };
        AddChild(_screen);
        var page = new RoleCreateControl { Name = "CreateRolePage" };
        _createPage = page;
        page.Submitted += (name, gender, headId) =>
        {
            if (_roleRequestPending) return;
            _roleRequestPending = true;
            page.SetBusy(true, "正在创建角色…");
            GameServices.Session.CreateRole(name, gender, headId);
        };
        page.Cancelled += () => ShowRoles();
        _screen.AddChild(page);
    }

    /// <summary>只有确认窗口的确认操作才发送删除请求。</summary>
    private void ConfirmDeleteRole(long roleId, string name)
    {
        if (_roleRequestPending || _screen is null) return;
        var dialog = new ConfirmationDialog { Name = "ConfirmDeleteRole", Title = "删除角色",
            DialogText = $"确定删除角色“{name}”？删除后无法恢复。", OkButtonText = "删除", CancelButtonText = "取消" };
        _screen.AddChild(dialog);
        dialog.Confirmed += () =>
        {
            if (_roleRequestPending) return;
            _roleRequestPending = true;
            if (_feedback is not null) _feedback.Text = "正在删除角色…";
            foreach (var node in _screen.FindChildren("*", "BaseButton", true, false)) ((BaseButton)node).Disabled = true;
            GameServices.Session.DeleteRole(roleId);
        };
        dialog.CloseRequested += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.Confirmed += dialog.QueueFree;
        dialog.PopupCentered();
    }

    /// <summary>缓存先更新列表，成功返回选角，失败恢复当前创建草稿。</summary>
    private void OnCreatedRole(CreateRoleRt response)
    {
        if (_createPage is null || !_roleRequestPending) return;
        _roleRequestPending = false;
        if (response.Success) ShowRoles();
        else _createPage.SetBusy(false, response.Error);
    }

    /// <summary>删除响应后从缓存重建列表并显示失败信息。</summary>
    private void OnDeletedRole(DeleteRoleRt response)
    {
        if (!_roleRequestPending || _createPage is not null) return;
        _roleRequestPending = false;
        ShowRoles(response.Success ? "" : response.Error);
    }

    /// <summary>由已有账号会话重置角色缓存并提交选角，成功响应前不创建世界。</summary>
    private void StartRole(long roleId)
    {
        if (_roleRequestPending || !GameServices.Session.IsLoggedIn || GameServices.Session.RoleId != 0) return;
        GameServices.Session.SelectRole(roleId);
        _roleRequestPending = true;
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
        UiSoundPlayer.Play(this, 1005);
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
        PresentEnteredWorld();
    }

    /// <summary>完整 V2 缓存接纳后复用世界展示，不构造旧响应。</summary>
    private void OnEnteredWorld(ProtocolV2.EnterWorldRt response)
    {
        if (!_roleRequestPending || !GameServices.Session.HasRole || response.State.RoleId != GameServices.Session.RoleId) return;
        PresentEnteredWorld();
    }

    /// <summary>两个入口共用物品素材预热和世界节点创建。</summary>
    private void PresentEnteredWorld()
    {
        _roleRequestPending = false;
        var bag = GameServices.Session.Bag;
        GameServices.Items.Preload(bag.Items.Select(item => item.ItemId)
            .Concat(GameServices.Session.Equipment.Items.Select(item => item.ItemId))
            .Select(id => GameServices.Config.Item.Items[id].IconAssetId));
        UiSoundPlayer.Play(this, 1005);
        GameServices.World.InitializeRole();
        BuildRoleWorld();
    }

    /// <summary>缓存处理成功移动响应后，提交世界同步操作。</summary>
    private void OnMoved(MoveRt response)
    {
        if (response.Success && response.RoleId == GameServices.Session.RoleId)
            GameServices.World.SyncRole();
    }

    /// <summary>缓存处理当前角色推送后，同步世界，不依赖 HUD 生命周期。</summary>
    private void OnRoleNotice(RoleStateNotice notice)
    {
        if (notice.State?.Profile?.RoleId == GameServices.Session.RoleId)
            GameServices.World.SyncRole();
    }

    /// <summary>资料推送写入缓存后同步世界角色外观。</summary>
    private void OnProfileNotice(RoleProfileNotice notice)
    {
        if (notice.Profile?.RoleId == GameServices.Session.RoleId) GameServices.World.SyncRole();
    }

    /// <summary>位置推送写入缓存后同步世界坐标。</summary>
    private void OnPositionNotice(RolePositionNotice notice)
    {
        if (notice.Position?.RoleId == GameServices.Session.RoleId) GameServices.World.SyncRole();
    }

    /// <summary>拾取最终结果只显示反馈，两侧状态已由原缓存通知维护。</summary>
    private void OnPickedUp(PickupItemRt response)
    {
        if (response.RoleId != GameServices.Session.RoleId) return;
        if (response.Success) UiSoundPlayer.Play(this, 1001);
        _hud?.ShowNetworkError(response.Success ? "" : response.Error);
    }

    /// <summary>地图缓存已处理通知后，只协调缺口重同步，不处理对象数据。</summary>
    private void OnMapNotice(MapAoiNotice notice)
    {
        if (notice.RoleId == GameServices.Session.RoleId) GameServices.Session.ResyncMapIfNeeded();
    }

    /// <summary>为服务已经初始化的世界建立地图、角色、摄像机与 HUD 表现。</summary>
    private void BuildRoleWorld()
    {
        _bgm?.PlayTrack(GameServices.World.MapConfig.BgmTrackId);
        _screen?.QueueFree();
        _screen = null;
        _feedback = null;
        _loginButton = null;
        _worldVisual = new Node2D { Name = "WorldVisual", YSortEnabled = true };
        AddChild(_worldVisual);
        _worldVisual.AddChild(new MapView { Name = "MapView" });
        _groundItems = new GroundItemsView { Name = "GroundItemsView", ZIndex = 1, YSortEnabled = true };
        _worldVisual.AddChild(_groundItems);
        _groundOverlay = new GroundItemOverlayControl(_groundItems) { Name = "GroundItemOverlay" };
        AddChild(_groundOverlay);
        if (!GameServices.Session.IsWorldReadOnly) _groundOverlay.PickupRequested += RequestPickup;
        _actor = new ActorView { Name = "ActorView", ZIndex = 1, Position = IsometricProjection.ToScreen(GameServices.World.RolePosition) };
        _worldVisual.AddChild(_actor);
        _actor.Present(GameServices.World.ActorState!);
        _camera = new Camera2D { Name = "WorldCamera", Position = IsometricProjection.ToScreen(GameServices.World.RolePosition), Enabled = true };
        _worldVisual.AddChild(_camera);

        _hudLayer = new CanvasLayer { Name = "HudLayer", Layer = 10 };
        AddChild(_hudLayer);
        _hud = new GameHud { Name = "GameHud" };
        _hudLayer.AddChild(_hud);
        _hud.GroundItemInfoChanged += visible => _groundOverlay?.SetInfoVisible(visible);
        _hud.AnchorMarkersChanged += (visible, color) =>
        {
            _actor?.SetAnchorMarker(visible, color);
            _groundItems?.SetAnchorMarkers(visible, color);
        };
        _actor.SetAnchorMarker(GameServices.Env.ShowEntityAnchors, Colors.Yellow);
        _groundItems.SetAnchorMarkers(GameServices.Env.ShowEntityAnchors, Colors.Yellow);
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
        if (!TryFlushBeforeLeavingRole(includeAccount: true)) return;
        ClearGame();
        GetTree().Quit();
    }

    /// <summary>离开角色或 V2 账号前同步落地；失败保留当前页面并提供重试反馈。</summary>
    private bool TryFlushBeforeLeavingRole(bool includeAccount = false)
    {
        if (_servicesLifetime is null || !GameServices.Session.HasRole &&
            !(includeAccount && GameServices.Session.IsWorldReadOnly && GameServices.Session.IsLoggedIn)) return true;
        try
        {
            GameServices.Net.FlushServerWrites();
            return true;
        }
        catch (Exception error)
        {
            if (_hud is not null) _hud.ShowNetworkError(error.Message);
            else if (_feedback is not null) _feedback.Text = error.Message;
            GD.PushError(error.ToString());
            return false;
        }
    }

    /// <summary>显示网络及事件监听错误，选角阶段失败时恢复可操作的角色列表。</summary>
    private void OnNetworkError(Exception error)
    {
        GameServices.Session.CancelAccountRequest();
        if (_createPage is not null)
        {
            _roleRequestPending = false;
            _createPage.SetBusy(false, error.Message);
        }
        else if (!GameServices.World.HasWorld && GameServices.Session.IsLoggedIn) ShowRoles(error.Message);
        else if (_feedback is not null) _feedback.Text = error.Message;
        else _hud?.ShowNetworkError(error.Message);
        if (_loginButton is not null) _loginButton.Disabled = false;
    }

    /// <summary>清理旧界面、世界和角色数据，保留账号会话与登录摘要。</summary>
    private void ClearGame()
    {
        _createPage = null;
        _roleRequestPending = false;
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
        _groundOverlay?.QueueFree();
        _groundOverlay = null;
        _worldVisual?.QueueFree();
        _worldVisual = null;
        _groundItems = null;
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

    /// <summary>应用本地窗口模式、普通窗口分辨率与音量。</summary>
    private void ApplySystemSettings()
    {
        var width = Math.Clamp(GameServices.Setting.Width, 800, 3840);
        var height = Math.Clamp(GameServices.Setting.Height, 600, 2160);
        DisplaySettings.Apply(GetWindow(), new Vector2I(width, height), GameServices.Setting.WindowMode);
        UiSoundPlayer.SetVolumes(GameServices.Setting.MasterVolume, GameServices.Setting.EffectsVolume);
    }

    /// <summary>创建指定字号的文本控件。</summary>
    private static Label Label(string content, int size)
    {
        var label = new Label { Text = content };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
