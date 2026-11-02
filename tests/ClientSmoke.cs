using Rpg.Net;
using Rpg.Config;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Settings;
using Rpg.Settings.Data;
using Rpg.Server;
using Rpg.Services;
using System.Collections.Generic;
using Rpg.Session.Data;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/client-ui 的菜单流程、client-session 的选角失败恢复、client-actor-presentation 的男女表现及 game-services 的启动清理与重建。</summary>
public partial class ClientSmoke : Node
{
    /// <summary>运行一次无界面场景集成检查并以退出码报告结果。</summary>
    public override async void _Ready()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "rpg-smoke-" + Guid.NewGuid());
        try
        {
            // game-services 无参启动：默认环境统一目录，重复启动不会替换本次服务。
            using (var defaults = GameServices.Initialize())
            {
                var env = GameServices.Env;
                if (env.ConfigDirectory != "res://config" || env.SchemaDirectory != "res://schemas" ||
                    env.SettingsDirectory != ProjectSettings.GlobalizePath("user://settings") ||
                    env.ServerDirectory != ProjectSettings.GlobalizePath("user://server"))
                    throw new InvalidOperationException("无参启动未使用统一的默认环境");
                try
                {
                    GameServices.Initialize();
                    throw new Exception("重复启动未被拒绝");
                }
                catch (InvalidOperationException) { }
                if (!ReferenceEquals(env, GameServices.Env))
                    throw new InvalidOperationException("重复启动替换了环境");
            }
            // game-services 启动失败清理：预期报告一次受控错误，随后正常场景仍能初始化。
            GD.Print("Checking expected startup failure: 模拟注册后启动失败");
            var failed = new DoubleStartupFailureController();
            AddChild(failed);
            if (GameServices.IsInitialized || !failed.TransportDisposed || failed.CreatedNet?.IsDisposed != true)
                throw new InvalidOperationException("注册后启动失败未释放全局服务及传输");
            if (!failed.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.Text == "模拟注册后启动失败"))
                throw new InvalidOperationException("启动失败未显示错误反馈");
            failed.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var main = new DoubleMainController();
            main.ServerDirectory = Path.Combine(temporary, "server");
            main.SettingsDirectory = temporary;
            main.EnvFilePath = Path.Combine(temporary, ".env");
            RoleTestData.Seed(main.ServerDirectory);
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var loginFields = main.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            var loginButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录");
            loginFields[1].Text = "wrong";
            loginButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            if (!main.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.Text.Contains("用户名或密码错误")))
                throw new InvalidOperationException("错误凭据没有失败反馈");
            if (GetAccountRoles() is not null) throw new InvalidOperationException("错误凭据创建了账号会话");
            loginFields[1].Text = "123";
            loginButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            var account = GetAccountRoles() ?? throw new InvalidOperationException("登录成功后未建立账号会话");
            if (!GameServices.Session.IsLoggedIn || GameServices.Session.HasRole || GameServices.Session.RoleId != 0)
                throw new InvalidOperationException("选角前账号摘要或空角色缓存不正确");
            var roleButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1001");
            // client-session 选角失败场景：坏存档不建立世界，恢复文件后可重试。
            var rolePath = Path.Combine(main.ServerDirectory, "1001.role.json");
            var validRole = File.ReadAllText(rolePath);
            File.WriteAllText(rolePath, "{broken");
            roleButton.EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            AssertGameCleared(main, account);
            if (!main.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("处理") || label.Text.Contains("失败")))
                throw new InvalidOperationException("选角失败未显示存档错误");
            File.WriteAllText(rolePath, validRole);
            roleButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1001");
            if (roleButton.Disabled) throw new InvalidOperationException("选角失败后角色按钮未恢复");
            roleButton.EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            var hud = main.GetChildren().OfType<CanvasLayer>().Single(layer => layer.Name == "HudLayer")
                .GetChildren().OfType<GameHud>().Single();
            AssertRadarTopRight(hud);
            var world = main.GetChildren().OfType<Node2D>().Single();
            var actor = world.GetChildren().OfType<ActorView>().Single();
            if (hud.GetChildren().OfType<PanelContainer>().Count() < 7)
                throw new InvalidOperationException("HUD 或基础面板未创建");
            if (!world.GetChildren().OfType<MapView>().Any())
                throw new InvalidOperationException("地图表现未创建");
            // client-ui：滚轮默认禁用，经真实 GM 开关启用；分类切换不丢失调试状态。
            var camera = world.GetNode<Camera2D>("WorldCamera");
            var wheelZoom = (CheckButton)hud.FindChild("MouseWheelZoomEnabled", true, false);
            var originalTarget = GameServices.World.RoleTarget;
            WheelAt(new Vector2(500, 300));
            if (wheelZoom.ButtonPressed || hud.MouseWheelZoomEnabled || camera.Zoom != Vector2.One ||
                GameServices.World.RoleTarget != originalTarget)
                throw new InvalidOperationException("滚轮缩放未默认关闭或改变了移动目标");
            var gmPanel = hud.GetNode<Control>("GM");
            Click(hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "GM"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var gmTabs = (TabContainer)gmPanel.FindChild("GmTabs", true, false);
            var tabBar = gmTabs.GetTabBar();
            if (gmTabs.GetTabCount() != 3 || gmTabs.CurrentTab != 0 ||
                gmTabs.GetTabTitle(0) != "世界调试" || gmTabs.GetTabTitle(1) != "角色调试" ||
                gmTabs.GetTabTitle(2) != "系统工具" ||
                !gmTabs.GetChild(0).IsAncestorOf(wheelZoom) ||
                !gmTabs.GetChild(1).IsAncestorOf(hud.FindChild("HealthPercent", true, false)) ||
                !gmTabs.GetChild(1).IsAncestorOf(hud.FindChild("MovementSpeed", true, false)) ||
                !gmTabs.GetChild(2).IsAncestorOf(hud.FindChild("OpenItemCatalog", true, false)) ||
                !gmTabs.GetChild(2).IsAncestorOf(hud.FindChild("ConsoleLogEnabled", true, false)))
                throw new InvalidOperationException("GM 功能分类或默认标签页错误");
            Click(wheelZoom);
            foreach (var index in new[] { 1, 2, 0 })
            {
                ClickAt(tabBar.GlobalPosition + tabBar.GetTabRect(index).GetCenter());
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (gmTabs.CurrentTab != index || wheelZoom.IsVisibleInTree() != (index == 0) ||
                    !wheelZoom.ButtonPressed || !hud.MouseWheelZoomEnabled)
                    throw new InvalidOperationException("GM 页签切换未隔离内容或丢失开关状态");
            }
            Click((Control)gmPanel.FindChild("ClosePanel", true, false));
            WheelAt(new Vector2(500, 300));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 1.1f)) throw new InvalidOperationException("滚轮未更新基础相机缩放");
            // client-ui、client-config-map：滚轮基础缩放与 GM 倍率相乘，边缘控件即时生效。
            var gmZoom = (HSlider)hud.FindChild("GmDisplayZoom", true, false);
            if (gmZoom.MinValue != 0.1 || gmZoom.MaxValue != 4 || gmZoom.Value != 1)
                throw new InvalidOperationException("GM 缩放范围或默认值错误");
            gmZoom.Value = 4;
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 4.4f)) throw new InvalidOperationException("GM 放大上限未生效");
            gmZoom.Value = 0.1;
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 0.11f)) throw new InvalidOperationException("GM 扩大观察范围下限未生效");
            gmZoom.Value = 0.5;
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 0.55f)) throw new InvalidOperationException("GM 倍率未叠加基础缩放");
            Click(hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "GM"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!wheelZoom.ButtonPressed || !hud.MouseWheelZoomEnabled || gmZoom.Value != 0.5)
                throw new InvalidOperationException("关闭重开 GM 丢失调试状态");
            WheelAt(wheelZoom.GetGlobalRect().GetCenter());
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 0.55f)) throw new InvalidOperationException("GM 页内滚轮穿透世界");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Click(wheelZoom);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Click((Control)gmPanel.FindChild("ClosePanel", true, false));
            if (gmPanel.Visible) throw new InvalidOperationException("停用滚轮后未点击到 GM 关闭按钮");
            WheelAt(new Vector2(500, 300));
            if (hud.MouseWheelZoomEnabled || !Mathf.IsEqualApprox(camera.Zoom.X, 0.55f) || gmZoom.Value != 0.5 ||
                GameServices.World.RoleTarget != originalTarget)
                throw new InvalidOperationException("关闭滚轮缩放未保留当前倍率或仍接收滚轮");
            Click(hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "GM"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Click(wheelZoom);
            if (!hud.MouseWheelZoomEnabled) throw new InvalidOperationException("再次打开 GM 后未点击到滚轮开关");
            Click((Control)gmPanel.FindChild("ClosePanel", true, false));
            var renderToggle = (CheckButton)hud.FindChild("ShowRenderAreaBoundary", true, false);
            var renderColor = (ColorPickerButton)hud.FindChild("RenderAreaBoundaryColor", true, false);
            var mapView = world.GetNode<MapView>("MapView");
            var renderArea = mapView.GetNode<RenderAreaBoundaryView>("RenderAreaBoundary");
            var aoiToggle = (CheckButton)hud.FindChild("ShowCurrentAoi", true, false);
            var aoiHighlight = mapView.GetNode<AoiHighlightView>("AoiHighlight");
            if (aoiToggle.ButtonPressed || aoiHighlight.Visible) throw new InvalidOperationException("GM AOI 高亮未默认关闭");
            aoiToggle.ButtonPressed = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!aoiHighlight.Visible || !ReferenceEquals(aoiHighlight.Snapshot, GameServices.Session.Map))
                throw new InvalidOperationException("GM AOI 高亮没有使用已接纳地图快照");
            aoiToggle.ButtonPressed = false;
            if (aoiHighlight.Visible) throw new InvalidOperationException("GM AOI 高亮关闭后仍显示");
            if (renderToggle.ButtonPressed || renderColor.Color != Colors.HotPink || renderArea.Visible)
                throw new InvalidOperationException("GM 绘制区域边缘默认状态错误");
            renderToggle.ButtonPressed = true;
            renderColor.Color = Colors.Cyan;
            renderColor.EmitSignal(ColorPickerButton.SignalName.ColorChanged, Colors.Cyan);
            if (!renderArea.Visible || renderArea.LineColor != Colors.Cyan)
                throw new InvalidOperationException("GM 绘制区域边缘开关或改色无效");
            renderToggle.ButtonPressed = false;
            if (renderArea.Visible) throw new InvalidOperationException("GM 绘制区域边缘未关闭");
            gmZoom.Value = 1;
            using (var wheel = new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Position = new Vector2(500, 300) })
                GetViewport().PushInput(wheel, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!Mathf.IsEqualApprox(camera.Zoom.X, 1f)) throw new InvalidOperationException(
                $"滚轮与 GM 倍率没有独立恢复：倍率 {camera.Zoom.X}，开关 {hud.MouseWheelZoomEnabled}，窗口 {gmPanel.Visible}");
            var manager = GameServices.World;
            if (!manager.HasWorld || !manager.HasRole)
                throw new InvalidOperationException("世界或角色实体未初始化");
            if (actor.Position != IsometricProjection.ToScreen(manager.RolePosition))
                throw new InvalidOperationException("角色表现未使用等轴投影");
            AssertBodyResource(actor, "male");
            // client-ecs-world 越界输入场景：服务入口与 ECS 使用相同地图裁剪规则。
            using var boundaryClick = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
            if (!manager.HandleInput(boundaryClick, new Vector2(-100, 40000)))
                throw new InvalidOperationException("世界服务未接受地图点击");
            manager.Tick(0);
            if (manager.RoleTarget != new Vector2(0, 31999))
                throw new InvalidOperationException("越界点击未使用地图数据裁剪目标");
            var click = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = new Vector2(500, 400)
            };
            var projectedClick = GetViewport().GetCanvasTransform().AffineInverse() * click.Position;
            var expectedTarget = IsometricProjection.ToLogical(projectedClick);
            GetViewport().PushInput(click, true);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (manager.RoleTarget is null)
                throw new InvalidOperationException("地图点击未转为世界 Action");
            if (manager.RoleTarget.Value.DistanceTo(expectedTarget) > 2)
                throw new InvalidOperationException("等轴地图点击未还原为逻辑移动目标");
            if (manager.ActorState!.Motion != RoleMotion.Run ||
                !actor.GetNode<AnimatedSprite2D>("Facing/Body").IsPlaying())
                throw new InvalidOperationException("点击移动未驱动玩家跑步动画");
            var characterButton = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .First(button => button.Text == "角色");
            characterButton.EmitSignal(Button.SignalName.Pressed);
            if (!hud.GetChildren().OfType<PanelContainer>().Any(panel => panel.Name == "角色" && panel.Visible))
                throw new InvalidOperationException("角色面板无法打开");
            // client-ui：全部 GM 设定经控件保存，并在文件不可替换时保持控件与世界表现。
            await ConfigurePersistentGm(hud);
            using (var locked = new FileStream(main.EnvFilePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                wheelZoom.ButtonPressed = false;
                gmZoom.Value = 0.75;
                aoiToggle.ButtonPressed = false;
                var healthPreview = (HSlider)hud.FindChild("HealthPercent", true, false);
                healthPreview.Value = 50;
                renderColor.Color = Colors.White;
                renderColor.EmitSignal(ColorPickerButton.SignalName.ColorChanged, Colors.White);
                if (!wheelZoom.ButtonPressed || gmZoom.Value != 0.5 || !aoiToggle.ButtonPressed ||
                    healthPreview.Value != 25 || renderColor.Color != Colors.Blue ||
                    ((Label)hud.FindChild("NetworkError", true, false)).Text == "")
                    throw new InvalidOperationException("GM 保存失败未恢复控件或显示原因");
            }
            // client-ui：正式菜单与角色切换，并恢复持久化 GM 设定。
            PressMenu(hud, "返回角色选择");
            await SmokeNetwork.WaitForAccount(this);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main, account);
            ((Button)main.FindChild("SelectRole1002", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            AssertPersistentGm(main, speedOverride: false);
            WheelAt(new Vector2(500, 300));
            if (!Mathf.IsEqualApprox(main.GetNode<Camera2D>("WorldVisual/WorldCamera").Zoom.X, 0.55f))
                throw new InvalidOperationException("重新进入世界后滚轮未恢复启用");
            AssertBodyResource(main.GetChildren().OfType<Node2D>().Single()
                .GetChildren().OfType<ActorView>().Single(), "female");
            PressMenu(main.GetNode<GameHud>("HudLayer/GameHud"), "返回登陆");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main, null);
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
            await SmokeNetwork.WaitForAccount(this);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetAccountRoles() is not { } nextAccount || ReferenceEquals(account, nextAccount))
                throw new InvalidOperationException("重新登录没有建立新的账号会话");
            ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            var finalHud = main.GetNode<GameHud>("HudLayer/GameHud");

            // session-events：HUD 离树后，正式移动仍由世界帧接纳。
            finalHud.GetParent().RemoveChild(finalHud);
            var moveTarget = GameServices.Session.Role.Position + new Vector2(30, 0);
            using (var move = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true })
                GameServices.World.HandleInput(move, moveTarget);
            var moveDeadline = DateTime.UtcNow.AddSeconds(3);
            while (GameServices.Session.Role.Position.DistanceTo(moveTarget) > 1 && DateTime.UtcNow < moveDeadline)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GameServices.Session.Role.Position.DistanceTo(moveTarget) > 1 || GameServices.World.RolePosition.DistanceTo(GameServices.Session.Role.Position) > 1)
                throw new InvalidOperationException("HUD 离树后权威位置未同步到世界");

            // game-services 退出后重新启动：旧网络不可用，新场景可再次登录和选角。
            var originalNet = GameServices.Net;
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            finalHud.Free();
            if (GameServices.IsInitialized || !originalNet.IsDisposed)
                throw new InvalidOperationException("主场景退出后全局服务未释放");
            main = new DoubleMainController();
            main.ServerDirectory = Path.Combine(temporary, "server");
            main.SettingsDirectory = temporary;
            main.EnvFilePath = Path.Combine(temporary, ".env");
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!GameServices.IsInitialized || ReferenceEquals(originalNet, GameServices.Net))
                throw new InvalidOperationException("主场景重建未注册独立网络服务");
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            if (GetAccountRoles() is null) throw new InvalidOperationException("主场景重建后无法登录");
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1001").EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            finalHud = main.GetNode<GameHud>("HudLayer/GameHud");
            AssertPersistentGm(main);
            Click(finalHud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "系统"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // client-ui 退出游戏场景：必须由菜单使本次 Godot 进程正常结束，后续帧继续执行则判定失败。
            GD.Print("Client smoke passed. Checking system menu exit...");
            Click(finalHud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false)
                .OfType<Button>().Single(button => button.Text == "退出游戏"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            throw new InvalidOperationException("退出游戏未关闭客户端");
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    /// <summary>等待固定帧选角响应创建 HUD，覆盖 client-session 的异步初始化顺序。</summary>
    private async Task WaitForHud(MainController main)
    {
        for (var frame = 0; frame < 120 && !main.HasNode("HudLayer/GameHud"); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!main.HasNode("HudLayer/GameHud")) throw new InvalidOperationException("选角响应后未建立 HUD");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>通过真实视口鼠标事件点击控件中心，覆盖菜单输入路由。</summary>
    private void Click(Control control) => ClickAt(control.GetGlobalRect().GetCenter());

    /// <summary>通过真实视口滚轮验收 client-ui 的世界开关和界面输入隔离。</summary>
    private void WheelAt(Vector2 position)
    {
        using var motion = new InputEventMouseMotion { Position = position };
        GetViewport().PushInput(motion, true);
        using var input = new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = position };
        GetViewport().PushInput(input, true);
        input.Pressed = false;
        GetViewport().PushInput(input, true);
    }

    /// <summary>通过 GM 控件保存各类开关、颜色、倍率和角色预览，验收 client-ui 持久化。</summary>
    private async System.Threading.Tasks.Task ConfigurePersistentGm(GameHud hud)
    {
        foreach (var name in new[] { "MouseWheelZoomEnabled", "ShowEntityAnchors", "ShowChunkBoundaries", "ShowCurrentAoi", "ShowRenderAreaBoundary" })
            ((CheckButton)hud.FindChild(name, true, false)).ButtonPressed = true;
        foreach (var (name, color) in new[] { ("EntityAnchorColor", Colors.Cyan), ("ChunkBoundaryColor", Colors.Red), ("RenderAreaBoundaryColor", Colors.Blue) })
        {
            var picker = (ColorPickerButton)hud.FindChild(name, true, false);
            picker.Color = color;
            picker.EmitSignal(ColorPickerButton.SignalName.ColorChanged, color);
        }
        ((HSlider)hud.FindChild("GmDisplayZoom", true, false)).Value = 0.5;
        ((HSlider)hud.FindChild("HealthPercent", true, false)).Value = 25;
        var toggle = (CheckButton)hud.FindChild("MovementSpeedOverride", true, false);
        toggle.ButtonPressed = true;
        var limit = DateTime.UtcNow.AddSeconds(5);
        while (toggle.Disabled && DateTime.UtcNow < limit) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ((HSlider)hud.FindChild("MovementSpeed", true, false)).Value = 300;
        while (toggle.Disabled && DateTime.UtcNow < limit) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (toggle.Disabled) throw new InvalidOperationException("GM 移速保存超时");
        ((SpinBox)hud.FindChild("ServerFlushIntervalSeconds", true, false)).Value = 30;
    }

    /// <summary>重入与重启恢复本地显示设定，移速只恢复当前角色的服务端覆盖。</summary>
    private static void AssertPersistentGm(MainController main, bool speedOverride = true)
    {
        foreach (var name in new[] { "MouseWheelZoomEnabled", "ShowEntityAnchors", "ShowChunkBoundaries", "ShowCurrentAoi", "ShowRenderAreaBoundary" })
            if (!((CheckButton)main.FindChild(name, true, false)).ButtonPressed)
                throw new InvalidOperationException("重入或重启未恢复 GM 开关：" + name);
        var actorAnchor = main.GetNode<EntityAnchorView>("WorldVisual/ActorView/EntityAnchor");
        var chunks = main.GetNode<ChunkBoundaryView>("WorldVisual/MapView/ChunkBoundaries");
        var render = main.GetNode<RenderAreaBoundaryView>("WorldVisual/MapView/RenderAreaBoundary");
        if (!Mathf.IsEqualApprox(main.GetNode<Camera2D>("WorldVisual/WorldCamera").Zoom.X, 0.5f) ||
            ((HSlider)main.FindChild("GmDisplayZoom", true, false)).Value != 0.5 ||
            ((HSlider)main.FindChild("HealthPercent", true, false)).Value != 25 ||
            ((ProgressBar)main.FindChild("HealthBar", true, false)).Value != 25 ||
            ((HSlider)main.FindChild("MovementSpeed", true, false)).Value != (speedOverride ? 300 : 180) ||
            ((CheckButton)main.FindChild("MovementSpeedOverride", true, false)).ButtonPressed != speedOverride ||
            ((SpinBox)main.FindChild("ServerFlushIntervalSeconds", true, false)).Value != 30 ||
            !actorAnchor.Visible || actorAnchor.MarkerColor != Colors.Cyan ||
            !chunks.Visible || chunks.LineColor != Colors.Red || !render.Visible || render.LineColor != Colors.Blue ||
            !main.GetNode<AoiHighlightView>("WorldVisual/MapView/AoiHighlight").Visible)
            throw new InvalidOperationException("重入或重启未恢复 GM 数值、颜色或世界表现");
    }

    /// <summary>发送完整鼠标点击，验证 UI 消费事件且不会触发地图移动。</summary>
    private void ClickAt(Vector2 position)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = position }, true);
        GetViewport().PushInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = true, Position = position }, true);
        GetViewport().PushInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = false, Position = position }, true);
    }

    /// <summary>通过真实菜单按钮提交离开操作。</summary>
    private static void PressMenu(GameHud hud, string title)
    {
        hud.FindChildren("*", "Button", true, false).OfType<Button>()
            .Single(button => button.Text == "系统").EmitSignal(Button.SignalName.Pressed);
        hud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false)
            .OfType<Button>().Single(button => button.Text == title).EmitSignal(Button.SignalName.Pressed);
    }

    /// <summary>读取服务公开的摘要身份，用于验证账号跨切角保留、重登后替换。</summary>
    private static IReadOnlyList<RoleSummaryPbData>? GetAccountRoles() =>
        GameServices.Session.IsLoggedIn ? GameServices.Session.Roles : null;

    /// <summary>验证世界与角色数据已清理，选角页保留原账号会话，登录页不保留会话。</summary>
    private static void AssertGameCleared(MainController main, IReadOnlyList<RoleSummaryPbData>? expectedSession)
    {
        if (main.GetChildren().OfType<Node2D>().Any() || main.HasNode("HudLayer") ||
            (expectedSession is null ? GetAccountRoles() is not null : GetAccountRoles() is not { } current || !current.Select(role => role.RoleId).SequenceEqual(expectedSession.Select(role => role.RoleId))) ||
            expectedSession is not null && (GameServices.Session.RoleId != 0 || GameServices.Session.HasRole || !GameServices.Session.IsLoggedIn))
            throw new InvalidOperationException("返回页面后世界、角色数据或账号会话生命周期不正确");
    }

    /// <summary>验证 client-ui 游戏 HUD 场景中的雷达始终贴近地图画面右上角。</summary>
    private static void AssertRadarTopRight(GameHud hud)
    {
        var radar = hud.GetChildren().OfType<PanelContainer>().Single(panel => panel.Name == "Radar");
        var viewport = hud.GetViewportRect().Size;
        if (Math.Abs(radar.Position.X + radar.Size.X - (viewport.X - 16)) > 1 ||
            Math.Abs(radar.Position.Y - 16) > 1 ||
            radar.Position.X < 0 || radar.Position.Y < 0)
            throw new InvalidOperationException("小地图雷达未位于地图画面右上角");
    }

    /// <summary>验证 client-actor-presentation 的选角流程使用对应性别的真实身体图集。</summary>
    private static void AssertBodyResource(ActorView actor, string gender)
    {
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var texture = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame) as AtlasTexture;
        if (texture?.Atlas is null || !texture.Atlas.ResourcePath.Contains($"/characters/{gender}/"))
            throw new InvalidOperationException("选角后没有使用对应性别的身体资源");
    }
}
