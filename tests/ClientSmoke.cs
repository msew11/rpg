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
using Rpg.World;
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
            loginFields[1].Text = "demo";
            loginButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            var account = GetAccountRoles() ?? throw new InvalidOperationException("登录成功后未建立账号会话");
            if (!GameServices.Session.IsLoggedIn || GameServices.Session.HasRole || GameServices.Session.RoleId != 0)
                throw new InvalidOperationException("选角前账号摘要或空角色缓存不正确");
            var roleButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1001");
            // client-session 选角失败场景：坏存档不建立世界，恢复文件后可重试。
            var rolePath = new RepoServerRole(main.ServerDirectory).GetRolePath(1001);
            var validRole = File.ReadAllText(rolePath);
            File.WriteAllText(rolePath, "{broken");
            roleButton.EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            AssertGameCleared(main, account);
            if (!main.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains(rolePath)))
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
            var systemButton = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .First(button => button.Text == "系统");
            var menuButtons = hud.GetChildren().OfType<PanelContainer>()
                .Single(panel => panel.Name == "menu")
                .FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
            if (!menuButtons.Select(button => button.Text).SequenceEqual(["角色", "背包", "大地图", "GM", "系统"]))
                throw new InvalidOperationException("系统菜单按钮顺序错误");
            menuButtons[3].EmitSignal(Button.SignalName.Pressed);
            var gmPanel = hud.GetChildren().OfType<PanelContainer>().Single(panel => panel.Name == "GM");
            var paths = gmPanel.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            if (!gmPanel.Visible || paths.Length != 2 ||
                paths[0].Text != Path.GetFullPath(Path.Combine(temporary, "system.json")) ||
                paths[1].Text != Path.GetFullPath(Path.Combine(temporary, "roles", "1001.json")) ||
                gmPanel.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Count(button => button.Text.StartsWith("打开")) != 2)
                throw new InvalidOperationException("GM 设置文件路径或定位入口错误");
            Click(systemButton);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var overlay = hud.GetNode<Control>("SystemMenuOverlay");
            var popup = overlay.GetNode<PanelContainer>("SystemMenu");
            var entries = popup.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
            var systemPanel = hud.GetChildren().OfType<PanelContainer>().Single(panel => panel.Name == "系统设置");
            if (!overlay.Visible || systemPanel.Visible ||
                !entries.Select(button => button.Text).SequenceEqual(["系统设置", "返回角色选择", "返回登陆", "退出游戏"]))
                throw new InvalidOperationException("系统按钮未展开指定顺序的四项菜单，或直接打开了设置");
            AssertSystemMenuBounds(hud, systemButton, popup);
            Click(systemButton);
            if (overlay.Visible) throw new InvalidOperationException("再次点击系统按钮未收起菜单");
            Click(systemButton);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = false }, true);
            if (overlay.Visible) throw new InvalidOperationException("Esc 未收起系统菜单");
            Click(systemButton);
            var previousTarget = manager.RoleTarget;
            ClickAt(new Vector2(20, 300));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (overlay.Visible || manager.RoleTarget != previousTarget)
                throw new InvalidOperationException("菜单外部点击未收起菜单，或穿透到世界移动");
            Click(systemButton);
            Click(entries[0]);
            if (overlay.Visible) throw new InvalidOperationException("选择系统设置后菜单未收起");
            var tabs = systemPanel.FindChildren("*", "TabContainer", true, false).OfType<TabContainer>().Single();
            if (!systemPanel.Visible || tabs.GetTabCount() != 3)
                throw new InvalidOperationException("系统设置页签未创建");
            var resolution = tabs.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().Single();
            resolution.Select(1);
            var saveButtons = tabs.FindChildren("*", "Button", true, false).OfType<Button>()
                .Where(button => button.Text == "应用并保存").ToArray();
            saveButtons[0].EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertRadarTopRight(hud);
            Click(systemButton);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertSystemMenuBounds(hud, systemButton, popup);
            ClickAt(new Vector2(20, 300));
            var volume = tabs.FindChildren("*", "HSlider", true, false).OfType<HSlider>().Single();
            volume.Value = 40;
            saveButtons[1].EmitSignal(Button.SignalName.Pressed);
            var persistedSystem = new SettingService(new EnvService(temporary), new Rpg.Services.LogService(Console.WriteLine));
            if (persistedSystem.Width != 1600 || Math.Abs(persistedSystem.MasterVolume - 0.4f) > 0.001f)
                throw new InvalidOperationException("显示或声音设置未保存");
            var secondBar = tabs.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>()
                .Single(check => check.Text == "显示技能栏 2");
            secondBar.ButtonPressed = true;
            secondBar.EmitSignal(CheckBox.SignalName.Toggled, true);
            if (!ReadRoleSetting(temporary, 1001).VisibleHotbars[1])
                throw new InvalidOperationException("角色技能栏设置未保存");
            if (ReadRoleSetting(temporary, 1002).VisibleHotbars[1])
                throw new InvalidOperationException("角色设置互相污染");
            Click(systemButton);
            Click(entries[1]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main, account);
            var secondRole = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1002");
            secondRole.EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            var secondHud = main.GetChildren().OfType<CanvasLayer>().Single(layer => layer.Name == "HudLayer")
                .GetChildren().OfType<GameHud>().Single();
            AssertBodyResource(main.GetChildren().OfType<Node2D>().Single()
                .GetChildren().OfType<ActorView>().Single(), "female");
            var secondHotbar = secondHud.GetChildren().OfType<PanelContainer>()
                .Single(panel => panel.Name == "hotbar2");
            if (secondHotbar.Visible)
                throw new InvalidOperationException("切换角色后沿用了上一角色的技能栏设置");
            var secondSystem = secondHud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "系统");
            secondSystem.EmitSignal(Button.SignalName.Pressed);
            secondHud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false)
                .OfType<Button>().Single(button => button.Text == "系统设置").EmitSignal(Button.SignalName.Pressed);
            var editHud = secondHud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "进入 HUD 调整");
            editHud.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var handle = secondHud.FindChildren("*", "Label", true, false).OfType<Label>()
                .Single(label => label.Text.StartsWith("技能栏 1  拖动"));
            var start = handle.GlobalPosition + new Vector2(20, 8);
            var end = start + new Vector2(40, -30);
            GetViewport().PushInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = true, Position = start }, true);
            GetViewport().PushInput(new InputEventMouseMotion
                { Position = end, Relative = end - start }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = false, Position = end }, true);
            var savedPosition = ReadRoleSetting(temporary, 1002).HudPositions["hotbar1"];
            if (Math.Abs(savedPosition.X - RoleSetting.DefaultPositions()["hotbar1"].X) < 0.001f)
                throw new InvalidOperationException("HUD 拖动位置未保存");

            // client-ui 布局变化场景：拖动整条菜单后，上拉框仍跟随系统按钮定位。
            var menuHandle = secondHud.FindChildren("*", "Label", true, false).OfType<Label>()
                .Single(label => label.Text.StartsWith("系统菜单  拖动"));
            var menuStart = menuHandle.GlobalPosition + new Vector2(20, 8);
            var menuEnd = menuStart + new Vector2(-80, -30);
            GetViewport().PushInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = true, Position = menuStart }, true);
            GetViewport().PushInput(new InputEventMouseMotion
                { Position = menuEnd, Relative = menuEnd - menuStart }, true);
            GetViewport().PushInput(new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = false, Position = menuEnd }, true);
            var savedMenu = ReadRoleSetting(temporary, 1002).HudPositions["menu"];
            if (Math.Abs(savedMenu.X - RoleSetting.DefaultPositions()["menu"].X) < 0.001f)
                throw new InvalidOperationException("菜单 HUD 拖动位置未保存");

            // client-ui 返回登录场景：释放账号会话，重新登录建立新的账号会话。
            Click(secondSystem);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var secondPopup = secondHud.GetNode<PanelContainer>("SystemMenuOverlay/SystemMenu");
            AssertSystemMenuBounds(secondHud, secondSystem, secondPopup);
            if (Math.Abs(secondPopup.GetGlobalRect().End.X - secondSystem.GetGlobalRect().End.X) > 1)
                throw new InvalidOperationException("拖动 HUD 后系统上拉框未跟随系统按钮");
            var secondEntries = secondHud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false)
                .OfType<Button>().ToArray();
            Click(secondEntries.Single(button => button.Text == "返回登陆"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main, null);
            if (GameServices.Session.IsLoggedIn || GameServices.Session.HasRole)
                throw new InvalidOperationException("返回登录后旧账号缓存未释放");
            var relogin = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录");
            Click(relogin);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            if (GetAccountRoles() is not { } nextAccount || ReferenceEquals(account, nextAccount))
                throw new InvalidOperationException("重新登录没有建立新的账号会话");
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Name == "SelectRole1001").EmitSignal(Button.SignalName.Pressed);
            main.FindChild("EnterRole", true, false).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
            var finalHud = main.GetNode<GameHud>("HudLayer/GameHud");
            if (!finalHud.GetNode<PanelContainer>("hotbar2").Visible)
                throw new InvalidOperationException("重新登录后未恢复角色设置");

            // session-events：HUD 离树后，主控制器仍把缓存更新同步到世界。
            finalHud.GetParent().RemoveChild(finalHud);
            finalHud.QueueFree();
            var authority = GameServices.Session.Role.ToPb();
            authority.Profile.WithRevision(GameServices.Session.Role.Revision + 1);
            authority.Position.WithRevision(GameServices.Session.Role.Revision + 1);
            authority.Position.X += 13;
            authority.Position.AcknowledgedAction++;
            GameServices.EventBus.Publish(new Rpg.Net.Message.RoleStateNotice { State = authority });
            GameServices.World.Tick(0);
            if (GameServices.World.RolePosition != GameServices.Session.Role.Position)
                throw new InvalidOperationException("HUD 离树后主动推送未同步到世界");
            authority = authority.Clone();
            authority.Position.WithRevision(GameServices.Session.Role.Revision + 1);
            authority.Position.X += 17;
            authority.Position.AcknowledgedAction++;
            GameServices.EventBus.Publish(new Rpg.Net.Message.MoveRt
            {
                Success = true, RoleId = authority.Position.RoleId,
                ActionId = authority.Position.AcknowledgedAction, Position = authority.Position
            });
            GameServices.World.Tick(0);
            if (GameServices.World.RolePosition != GameServices.Session.Role.Position)
                throw new InvalidOperationException("HUD 离树后移动响应未同步到世界");

            // game-services 退出后重新启动：旧网络不可用，新场景可再次登录和选角。
            var originalNet = GameServices.Net;
            main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GameServices.IsInitialized || !originalNet.IsDisposed)
                throw new InvalidOperationException("主场景退出后全局服务未释放");
            main = new DoubleMainController();
            main.ServerDirectory = Path.Combine(temporary, "server");
            main.SettingsDirectory = temporary;
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

    /// <summary>发送完整鼠标点击，验证 UI 消费事件且不会触发地图移动。</summary>
    private void ClickAt(Vector2 position)
    {
        GetViewport().PushInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = true, Position = position }, true);
        GetViewport().PushInput(new InputEventMouseButton
            { ButtonIndex = MouseButton.Left, Pressed = false, Position = position }, true);
    }

    /// <summary>验证 client-ui 布局变化场景的菜单位于按钮上方且不超出视口。</summary>
    private static void AssertSystemMenuBounds(GameHud hud, Button button, PanelContainer popup)
    {
        var rect = popup.GetGlobalRect();
        var viewport = hud.GetViewportRect();
        if (rect.Position.X < 0 || rect.Position.Y < 0 || rect.End.X > viewport.End.X + 1 ||
            rect.End.Y > viewport.End.Y + 1 || rect.End.Y > button.GlobalPosition.Y)
            throw new InvalidOperationException("系统菜单未向上展开或超出了视口");
    }

    /// <summary>按 client-settings 的公开文件格式验收保存结果，不依赖内部读写方法。</summary>
    private static RoleSetting ReadRoleSetting(string directory, long roleId)
    {
        var path = Path.Combine(directory, "roles", $"{roleId}.json");
        return File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<RoleSetting>(File.ReadAllText(path))!
            : new RoleSetting();
    }

    /// <summary>读取服务公开的摘要身份，用于验证账号跨切角保留、重登后替换。</summary>
    private static IReadOnlyList<RoleSummaryPbData>? GetAccountRoles() =>
        GameServices.Session.IsLoggedIn ? GameServices.Session.Roles : null;

    /// <summary>验证世界与角色数据已清理，选角页保留原账号会话，登录页不保留会话。</summary>
    private static void AssertGameCleared(MainController main, IReadOnlyList<RoleSummaryPbData>? expectedSession)
    {
        if (main.GetChildren().OfType<Node2D>().Any() || main.HasNode("HudLayer") ||
            !ReferenceEquals(GetAccountRoles(), expectedSession) ||
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
