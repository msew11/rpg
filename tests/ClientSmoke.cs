using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Godot;
using Rpg.Settings;
using Rpg.Settings.Data;
using Rpg.UI;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/client-ui 的系统上拉菜单、返回与退出，以及 client-actor-presentation 的男女玩家表现流程。</summary>
public partial class ClientSmoke : Node
{
    /// <summary>运行一次无界面场景集成检查并以退出码报告结果。</summary>
    public override async void _Ready()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "rpg-smoke-" + Guid.NewGuid());
        try
        {
            var scene = GD.Load<PackedScene>("res://scenes/main.tscn");
            var main = scene.Instantiate<MainController>();
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            typeof(MainController).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(main, new RepoSettings(temporary));
            var loginFields = main.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            var loginButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录");
            loginFields[1].Text = "wrong";
            loginButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            if (!main.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.Text.Contains("用户名或密码错误")))
                throw new InvalidOperationException("错误凭据没有失败反馈");
            loginFields[1].Text = "demo";
            loginButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            var roleButton = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.Contains("ID 1001"));
            roleButton.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var hud = main.GetChildren().OfType<CanvasLayer>().Single(layer => layer.Name == "HudLayer")
                .GetChildren().OfType<GameHud>().Single();
            AssertRadarTopRight(hud);
            var world = main.GetChildren().OfType<Node2D>().Single();
            var actor = world.GetChildren().OfType<ActorView>().Single();
            if (hud.GetChildren().OfType<PanelContainer>().Count() < 7)
                throw new InvalidOperationException("HUD 或基础面板未创建");
            if (!world.GetChildren().OfType<MapView>().Any())
                throw new InvalidOperationException("地图表现未创建");
            var manager = (WorldManager)typeof(MainController)
                .GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            if (!manager.HasWorld || !manager.HasRole)
                throw new InvalidOperationException("世界或角色实体未初始化");
            if (actor.Position != IsometricProjection.ToScreen(manager.RolePosition))
                throw new InvalidOperationException("角色表现未使用等轴投影");
            AssertBodyResource(actor, "male");
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
                !entries.Select(button => button.Text).SequenceEqual(["系统设置", "退出游戏", "返回登录", "返回角色选择"]))
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
            var persistedSystem = new RepoSettings(temporary).LoadSystem();
            if (persistedSystem.Width != 1600 || Math.Abs(persistedSystem.MasterVolume - 0.4f) > 0.001f)
                throw new InvalidOperationException("显示或声音设置未保存");
            var secondBar = tabs.FindChildren("*", "CheckBox", true, false).OfType<CheckBox>()
                .Single(check => check.Text == "显示技能栏 2");
            secondBar.ButtonPressed = true;
            secondBar.EmitSignal(CheckBox.SignalName.Toggled, true);
            if (!new RepoSettings(temporary).LoadRole(1001).VisibleHotbars[1])
                throw new InvalidOperationException("角色技能栏设置未保存");
            if (new RepoSettings(temporary).LoadRole(1002).VisibleHotbars[1])
                throw new InvalidOperationException("角色设置互相污染");
            Click(systemButton);
            Click(entries[3]);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main);
            var secondRole = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.Contains("ID 1002"));
            secondRole.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
            var savedPosition = new RepoSettings(temporary).LoadRole(1002).HudPositions["hotbar1"];
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
            var savedMenu = new RepoSettings(temporary).LoadRole(1002).HudPositions["menu"];
            if (Math.Abs(savedMenu.X - RoleSetting.DefaultPositions()["menu"].X) < 0.001f)
                throw new InvalidOperationException("菜单 HUD 拖动位置未保存");

            // client-ui 返回登录场景：退出世界后可重新登录并进入独立角色会话。
            Click(secondSystem);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var secondPopup = secondHud.GetNode<PanelContainer>("SystemMenuOverlay/SystemMenu");
            AssertSystemMenuBounds(secondHud, secondSystem, secondPopup);
            if (Math.Abs(secondPopup.GetGlobalRect().End.X - secondSystem.GetGlobalRect().End.X) > 1)
                throw new InvalidOperationException("拖动 HUD 后系统上拉框未跟随系统按钮");
            var secondEntries = secondHud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false)
                .OfType<Button>().ToArray();
            Click(secondEntries.Single(button => button.Text == "返回登录"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            AssertGameCleared(main);
            var relogin = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "登录");
            Click(relogin);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.Contains("ID 1001")).EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var finalHud = main.GetNode<GameHud>("HudLayer/GameHud");
            if (!finalHud.GetNode<PanelContainer>("hotbar2").Visible)
                throw new InvalidOperationException("重新登录后未恢复角色设置");
            Click(finalHud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "系统"));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // client-ui 退出游戏场景：必须由菜单使本次 Godot 进程正常结束，后续帧继续执行则判定失败。
            Directory.Delete(temporary, true);
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

    /// <summary>验证 client-ui 返回页面场景已移除世界、HUD 和活动会话。</summary>
    private static void AssertGameCleared(MainController main)
    {
        if (main.GetChildren().OfType<Node2D>().Any() || main.HasNode("HudLayer") ||
            typeof(MainController).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main) is not null)
            throw new InvalidOperationException("返回页面后旧世界、HUD 或会话仍然存在");
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
