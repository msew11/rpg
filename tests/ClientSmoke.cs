using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Godot;
using Rpg.Settings;
using Rpg.UI;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/client-ui 的 Godot 场景可创建完整游戏 HUD。</summary>
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
            systemButton.EmitSignal(Button.SignalName.Pressed);
            var systemPanel = hud.GetChildren().OfType<PanelContainer>().Single(panel => panel.Name == "系统");
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
            characterButton.EmitSignal(Button.SignalName.Pressed);
            var changeRole = hud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "切换角色");
            changeRole.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var secondRole = main.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text.Contains("ID 1002"));
            secondRole.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var secondHud = main.GetChildren().OfType<CanvasLayer>().Single(layer => layer.Name == "HudLayer")
                .GetChildren().OfType<GameHud>().Single();
            var secondHotbar = secondHud.GetChildren().OfType<PanelContainer>()
                .Single(panel => panel.Name == "hotbar2");
            if (secondHotbar.Visible)
                throw new InvalidOperationException("切换角色后沿用了上一角色的技能栏设置");
            var secondSystem = secondHud.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "系统");
            secondSystem.EmitSignal(Button.SignalName.Pressed);
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
            if (Math.Abs(savedPosition.X - DataRoleSetting.DefaultPositions()["hotbar1"].X) < 0.001f)
                throw new InvalidOperationException("HUD 拖动位置未保存");
            GD.Print("Client smoke passed.");
            GetTree().Quit();
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
}
