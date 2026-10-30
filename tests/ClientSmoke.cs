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
            // client-ui：正式菜单与角色切换；窗口布局、设置和 GM 由各专用烟测覆盖。
            PressMenu(hud, "返回角色选择");
            await SmokeNetwork.WaitForAccount(this);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main, account);
            ((Button)main.FindChild("SelectRole1002", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await WaitForHud(main);
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
