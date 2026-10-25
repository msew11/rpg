using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Settings.Data;
using Rpg.Tests.Doubles;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/client-settings 的窗口保存、设置分类与快捷键真实输入。</summary>
public partial class RoleSettingsSmoke : Node
{
    /// <summary>通过真实标题拖动与键盘事件验证重启恢复、角色隔离和非法绑定。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-role-settings-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            main = await Start(directory);
            var hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            // client-ui：普通小地图不显示标题，调整框仍保留名称。
            var radar = hud.GetNode<Control>("Radar");
            Check(!radar.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "小地图") &&
                radar.GetNode<HudEditOverlay>("HudEditOverlay").Title == "小地图", "小地图标题或调整名称错误");
            // client-ui：地图与 HUD 外框重合，不能残留外层黑框及底部空带。
            var map = radar.FindChildren("*", "Control", true, false).OfType<MiniMapControl>().Single();
            Check(radar.GetGlobalRect().IsEqualApprox(map.GetGlobalRect()) &&
                radar.GetThemeStylebox("panel") is StyleBoxEmpty, "小地图外层仍有边距、黑框或空带");
            await VerifyWindowCoexistence(hud);
            var cursor = main.GetNode<PointerCursor>("PointerCursor");
            var target = GameServices.World.RoleTarget;
            foreach (var (title, key, action) in new[] { ("角色", Key.C, "character"), ("背包", Key.B, "bag"), ("大地图", Key.M, "map") })
            {
                SendKey(key);
                await Frames();
                var panel = hud.GetNode<Control>(title);
                Check(panel.Visible, "默认快捷键没有打开 " + title);
                var header = panel.FindChildren("PanelHeader", "Control", true, false).OfType<Control>().Single();
                var before = panel.Position;
                var start = header.GetGlobalRect().Position + new Vector2(18, header.Size.Y / 2);
                // client-settings：真实命中标题才显示 Fetch，拖离标题仍保持握手。
                Motion(start); await Frames();
                Check(cursor.IsWindowHovering && header.MouseDefaultCursorShape == Control.CursorShape.Arrow &&
                    cursor.CurrentHotspot.IsEqualApprox(new Vector2(7.5f, 7.5f)), "标题未使用 Fetch 开手 " + title);
                var openHand = cursor.CurrentTexture;
                Mouse(start, true);
                await Frames();
                Check(cursor.IsWindowDragging && cursor.CurrentTexture != openHand, "按下未显示窗口握手 " + title);
                Motion(start + new Vector2(-70, 45));
                await Frames();
                // 移动标题随鼠标同步移动；视口边缘裁剪使鼠标实际离开标题。
                Motion(new Vector2(-30, -30)); await Frames();
                Check(cursor.IsWindowDragging && !cursor.IsWindowHovering && cursor.CurrentTexture != openHand,
                    "拖离标题丢失握手 " + title);
                Motion(start + new Vector2(-70, 45));
                Mouse(start + new Vector2(-70, 45), false);
                await Frames();
                Check(!cursor.IsWindowDragging && cursor.IsWindowHovering && cursor.CurrentTexture == openHand,
                    "释放后未恢复标题开手 " + title);
                Check(panel.Position.DistanceTo(before + new Vector2(-70, 45)) < 1, "拖动位置未保持 " + title);
                var saved = GameServices.Setting.GetWindowPosition(action);
                Check(saved is not null, "拖动未保存 " + title);
                SendKey(key);
                await Frames();
                Check(!panel.Visible, "快捷键没有关闭 " + title);
                SendKey(key);
                await Frames();
                Check(panel.Position.DistanceTo(before + new Vector2(-70, 45)) < 1, "重开位置丢失 " + title);
                var close = header.FindChildren("*", "Button", true, false).OfType<Button>().Single();
                Motion(close.GetGlobalRect().GetCenter()); await Frames();
                Check(!cursor.IsWindowHovering, "关闭按钮误显示拖动开手 " + title);
                start = header.GetGlobalRect().Position + new Vector2(18, header.Size.Y / 2);
                Motion(start); Mouse(start, true); await Frames();
                cursor.Notification((int)NotificationApplicationFocusOut);
                panel.GetChildren().OfType<WindowDragControl>().Single().Notification((int)NotificationApplicationFocusOut);
                await Frames();
                Check(!cursor.IsWindowDragging && !cursor.IsWindowHovering, "失焦残留窗口 Fetch " + title);
                Mouse(start, false);
                cursor.Notification((int)NotificationApplicationFocusIn);
                Motion(start); Mouse(start, true); await Frames();
                panel.Hide(); await Frames();
                Check(!cursor.IsWindowDragging && !cursor.IsWindowHovering, "关闭残留窗口 Fetch " + title);
                Mouse(start, false);
                SendKey(key); await Frames();
                SendKey(key);
            }
            Check(GameServices.World.RoleTarget == target, "窗口拖动穿透地面");
            Press(hud, "系统"); Press(hud, "角色设置");
            await Frames();
            var rolePanel = hud.GetNode<Control>("角色设置");
            var tabs = (TabContainer)rolePanel.FindChild("RoleSettingTabs", true, false);
            Check(tabs.GetTabTitle(0) == "界面" && tabs.GetTabTitle(1) == "快捷键", "角色设置分类错误");
            var systemTabs = hud.GetNode("系统设置").FindChildren("*", "TabContainer", true, false).OfType<TabContainer>().Single();
            Check(systemTabs.GetTabCount() == 2, "系统设置仍包含 HUD 页签");
            tabs.CurrentTab = 1;
            await Frames();
            var bind = (Button)rolePanel.FindChild("Shortcut_character", true, false);
            Click(bind.GetGlobalRect().GetCenter());
            Check(bind.ButtonPressed, "点击后未高亮");
            foreach (var invalid in new[] { Key.Escape, Key.Shift, Key.Left, Key.B })
            {
                SendKey(invalid);
                Check(bind.ButtonPressed && GameServices.Setting.GetShortcut("character").Key == Key.C, "非法或冲突输入解除等待");
            }
            SendKey(Key.F4, alt: true);
            Check(bind.ButtonPressed, "系统关闭组合未被拦截");
            // client-settings：写盘失败不更换当前绑定，并允许保持高亮重试。
            var blockedWrite = GameServices.Setting.RolePath + ".tmp";
            Directory.CreateDirectory(blockedWrite);
            SendKey(Key.R, ctrl: true);
            Check(bind.ButtonPressed && GameServices.Setting.GetShortcut("character").Key == Key.C, "写盘失败后绑定提前生效");
            Directory.Delete(blockedWrite);
            SendKey(Key.R, ctrl: true);
            Check(!bind.ButtonPressed && bind.Text == "Ctrl+R", "合法绑定未保存或解除高亮");
            // client-settings：重复事件、切页和关闭均不能遗留录入状态。
            Click(bind.GetGlobalRect().GetCenter());
            SendKey(Key.T, echo: true);
            Check(bind.ButtonPressed, "键盘重复事件完成了绑定");
            tabs.CurrentTab = 0;
            Check(!bind.ButtonPressed, "切换页签未取消高亮");
            tabs.CurrentTab = 1;
            await Frames();
            Click(bind.GetGlobalRect().GetCenter());
            Press(rolePanel, "关闭");
            Check(!bind.ButtonPressed, "关闭窗口未取消高亮");
            SendKey(Key.C);
            Check(!hud.GetNode<Control>("角色").Visible, "旧快捷键仍生效");
            SendKey(Key.R, ctrl: true);
            Check(hud.GetNode<Control>("角色").Visible, "新快捷键未立即生效");
            SendKey(Key.R, ctrl: true);
            var chat = (LineEdit)hud.FindChild("CommandInput", true, false);
            chat.GrabFocus();
            SendKey(Key.B);
            Check(!hud.GetNode<Control>("背包").Visible, "聊天输入触发了快捷键");
            chat.ReleaseFocus();
            var roleId = GameServices.Session.RoleId;
            GameServices.Setting.SelectRole(roleId + 10000);
            Check(GameServices.Setting.GetWindowPosition("character") is null && GameServices.Setting.GetShortcut("character").Key == Key.C,
                "角色设置未隔离");
            // client-settings：旧设置不含新增字段时回填默认；非法与重复绑定回退。
            var legacyPath = GameServices.Setting.RolePath;
            File.WriteAllText(legacyPath, "{\"VisibleHotbars\":[true,true,false]}");
            GameServices.Setting.SelectRole(roleId + 10000);
            Check(GameServices.Setting.IsHotbarVisible(2) && GameServices.Setting.GetShortcut("bag").Key == Key.B,
                "旧设置回填破坏了已有 HUD 或默认绑定");
            File.WriteAllText(legacyPath, "{\"Shortcuts\":{\"character\":{\"Key\":66},\"bag\":{\"Key\":66},\"map\":{\"Key\":0}},\"WindowPositions\":null}");
            GameServices.Setting.SelectRole(roleId + 10000);
            Check(GameServices.Setting.GetShortcut("character").Key == Key.C && GameServices.Setting.GetShortcut("map").Key == Key.M,
                "非法或重复存档绑定没有回退默认");
            GameServices.Setting.SelectRole(roleId);
            Check(GameServices.Setting.GetShortcut("character").Ctrl, "重选角色丢失快捷键");
            var position = GameServices.Setting.GetWindowPosition("character")!;
            main.QueueFree(); await Frames(); main = null;
            main = await Start(directory);
            hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            SendKey(Key.R, ctrl: true); await Frames();
            var restored = hud.GetNode<Control>("角色");
            Check(restored.Visible && restored.Position.DistanceTo(GetViewport().GetVisibleRect().Size * new Vector2(position.X, position.Y)) < 1,
                "重启未恢复快捷键与位置");
            GetWindow().Size = new Vector2I(480, 270);
            GetWindow().ContentScaleSize = new Vector2I(480, 270);
            await Frames();
            Check(GetViewport().GetVisibleRect().Encloses(restored.GetGlobalRect()), "缩小窗口后越界");
            GD.Print("Role settings smoke passed: drag, reopen, restart, role isolation, tabs, invalid/conflicting capture, shortcuts and chat isolation.");
            main.QueueFree(); await Frames(); main = null;
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree(); await Frames();
            GetTree().Quit(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>验收 client-ui：多个独立窗口与普通面板共存，单独开关及置前不影响其他窗口。</summary>
    private async Task VerifyWindowCoexistence(GameHud hud)
    {
        var titles = new[] { "角色", "背包", "大地图" };
        foreach (var key in new[] { Key.C, Key.B, Key.M }) { SendKey(key); await Frames(); }
        var panels = titles.Select(title => hud.GetNode<Control>(title)).ToArray();
        Check(panels.All(panel => panel.Visible), "快捷键打开窗口仍互斥");
        var positions = panels.Select(panel => panel.Position).ToArray();
        SendKey(Key.B); await Frames();
        Check(panels[0].Visible && !panels[1].Visible && panels[2].Visible, "关闭背包影响其他窗口");
        Press(hud, "背包"); await Frames();
        Check(panels.All(panel => panel.Visible) && panels[1].GetIndex() > panels[2].GetIndex(), "按钮重开未共存或置前");
        foreach (var title in new[] { "GM", "系统设置", "角色设置", "图鉴" })
        {
            if (title == "图鉴") { Press(hud, "GM"); Press(hud, "查看道具图鉴"); }
            else if (title.EndsWith("设置")) { Press(hud, "系统"); Press(hud, title); }
            else Press(hud, title);
            await Frames();
            var other = hud.GetNode<Control>(title == "图鉴" ? "ItemCatalog" : title);
            Check(other.Visible && panels.All(panel => panel.Visible), "其他面板关闭独立窗口：" + title);
            SendKey(Key.C); await Frames();
            Check(other.Visible && !panels[0].Visible && panels[1].Visible && panels[2].Visible, "关闭角色影响其他面板");
            SendKey(Key.C); await Frames();
            Check(other.Visible && panels[0].Visible && panels[0].GetIndex() > other.GetIndex(), "打开角色关闭其他面板或未置前");
            for (var index = 0; index < panels.Length; index++)
                Check(panels[index].Position.IsEqualApprox(positions[index]), "共存切换改变窗口位置");
            other.Hide();
        }
        foreach (var panel in panels) panel.Hide();
    }

    /// <summary>启动隔离环境并进入固定验收角色。</summary>
    private async Task<DoubleMainController> Start(string directory)
    {
        GetWindow().Size = new Vector2I(1280, 720);
        GetWindow().ContentScaleSize = new Vector2I(1280, 720);
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
        AddChild(main); await Frames();
        Press(main, "登录"); await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Godot.Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Godot.Button.SignalName.Pressed);
        await Frames();
        return main;
    }

    /// <summary>提交包含修饰状态的真实键盘按下与松开。</summary>
    private void SendKey(Key key, bool ctrl = false, bool alt = false, bool echo = false)
    {
        using var input = new InputEventKey { Keycode = key, Pressed = true, CtrlPressed = ctrl, AltPressed = alt, Echo = echo };
        GetViewport().PushInput(input, true);
        input.Pressed = false; input.Echo = false;
        GetViewport().PushInput(input, true);
    }

    /// <summary>向视口提交鼠标按下或松开。</summary>
    private void Mouse(Vector2 point, bool pressed)
    {
        using var input = new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = pressed };
        GetViewport().PushInput(input, true);
    }

    /// <summary>向视口提交持续拖动事件。</summary>
    private void Motion(Vector2 point)
    {
        using var input = new InputEventMouseMotion { Position = point, ButtonMask = MouseButtonMask.Left };
        GetViewport().PushInput(input, true);
    }

    /// <summary>通过实际按钮区域完成点击。</summary>
    private void Click(Vector2 point) { Mouse(point, true); Mouse(point, false); }

    /// <summary>通过业务按钮打开面板或关闭窗口。</summary>
    private static void Press(Node root, string text) => root.FindChildren("*", "Button", true, false).OfType<Button>()
        .Single(button => button.Text == text).EmitSignal(Godot.Button.SignalName.Pressed);

    /// <summary>等待网络与容器布局稳定。</summary>
    private async Task Frames()
    {
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>任何验收失败均终止并返回非零退出码。</summary>
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
