using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/client-ui 的弹窗恢复、缩放滚动、关闭和 HUD 拖动边界。</summary>
public partial class UiBoundsSmoke : Node
{
    /// <summary>使用隔离存档和真实视口输入覆盖负坐标与窗口缩小的回归风险。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-ui-bounds-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            main = await StartMain(directory);
            var hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            var target = GameServices.World.RoleTarget;
            foreach (var title in new[] { "角色", "大地图", "GM", "系统设置" })
            {
                Open(hud, title);
                var panel = hud.GetNode<ViewportPanelControl>(title);
                foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(640, 360), new Vector2I(480, 270), new Vector2I(1280, 720) })
                {
                    Resize(size);
                    await Frames();
                    AssertInside(panel, title);
                    Check(panel.GetGlobalRect().GetCenter().IsEqualApprox(GetViewport().GetVisibleRect().GetCenter()), "弹窗未按实际视口居中");
                    var close = (Button)panel.FindChild("ClosePanel", true, false);
                    AssertInside(close, "关闭按钮");
                    Check(close.IsVisibleInTree(), "关闭按钮不可见");
                    var scroll = (ScrollContainer)panel.FindChild("PanelContentScroll", true, false);
                    if (panel.Content.GetCombinedMinimumSize().Y > scroll.Size.Y)
                    {
                        scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
                        await Frames();
                        Check(scroll.ScrollVertical > 0, "内容溢出后无法纵向滚动");
                    }
                    if (panel.Content.GetCombinedMinimumSize().X > scroll.Size.X)
                    {
                        scroll.ScrollHorizontal = (int)scroll.GetHScrollBar().MaxValue;
                        await Frames();
                        Check(scroll.ScrollHorizontal > 0, "内容溢出后无法横向滚动");
                    }
                    if (OS.GetCmdlineUserArgs().Contains("--capture-ui-bounds") && DisplayServer.GetName() != "headless" &&
                        ((title == "角色" && size.X == 1280) || (title == "系统设置" && size.X == 480)))
                    {
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng($"build/ui-bounds-{size.X}.png");
                    }
                }
                panel.Position = new Vector2(-500, -400);
                await Frames();
                AssertInside(panel, "已有越界位置恢复");
                Click(((Button)panel.FindChild("ClosePanel", true, false)).GetGlobalRect().GetCenter());
                await Frames();
                Check(!panel.Visible, "真实点击无法关闭弹窗");
            }

            Open(hud, "GM");
            Press(hud, "查看道具图鉴");
            Resize(new Vector2I(480, 270));
            await Frames();
            var catalog = hud.FindChildren("*", "Control", true, false).OfType<ItemCatalogControl>().Single();
            AssertInside(catalog, "小窗口图鉴");
            var catalogClose = (Button)catalog.FindChild("CloseCatalog", true, false);
            AssertInside(catalogClose, "图鉴关闭按钮");
            Click(catalogClose.GetGlobalRect().GetCenter());
            await Frames();
            Check(!catalog.Visible, "小窗口图鉴无法关闭");
            Resize(new Vector2I(1280, 720));
            await Frames();
            Open(hud, "系统设置");
            Press(hud, "进入 HUD 调整");
            hud.GetNode<Control>("系统设置").Hide();
            await Frames();
            var portrait = hud.GetNode<Control>("portrait");
            var handle = portrait.GetNode<HudEditOverlay>("HudEditOverlay");
            var before = portrait.Position;
            var start = handle.GetGlobalRect().GetCenter();
            Button(start, true);
            Move(start + new Vector2(10, 8));
            Check(portrait.Position.IsEqualApprox(before + new Vector2(10, 8)), "整区拖动局部坐标导致面板跳动");
            Move(new Vector2(-1000, -1000));
            AssertInside(portrait, "拖出左上边界");
            await Frames();
            Check(main.GetNode<PointerCursor>("PointerCursor").IsHudDragging, "拖出绿色区域未保持握手");
            Move(new Vector2(5000, 5000));
            AssertInside(portrait, "拖出右下边界");
            Button(new Vector2(5000, 5000), false);
            await Frames();
            Check(!main.GetNode<PointerCursor>("PointerCursor").IsHudDragging, "区域外松开后残留握手");
            // client-ui：覆盖菜单按钮内容区也只能拖动，不触发系统菜单。
            var menu = hud.GetNode<Control>("menu");
            var systemButton = menu.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "系统");
            var menuBefore = menu.Position;
            var menuStart = systemButton.GetGlobalRect().GetCenter();
            Button(menuStart, true);
            Move(menuStart + new Vector2(-30, -80));
            Button(menuStart + new Vector2(-30, -80), false);
            Check(menu.Position.IsEqualApprox(menuBefore + new Vector2(-30, -80)), "菜单按钮内容区无法拖动");
            Check(!hud.GetNode<Control>("SystemMenuOverlay").Visible, "菜单拖动触发了系统按钮");
            // 调整覆盖按钮后仍能重新打开设置，完成与取消入口始终可达。
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
            Check(hud.GetNode<Control>("系统设置").Visible, "调整中 Esc 未重新打开系统设置");
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
            Check(!hud.GetNode<Control>("系统设置").Visible, "调整中 Esc 未收起系统设置");
            foreach (var key in new[] { "portrait", "hotbar1", "menu" }) GameServices.Setting.SetHudPosition(key, 1, 1);
            GameServices.Setting.SaveCurrentRole();
            foreach (var size in new[] { new Vector2I(480, 270), new Vector2I(1920, 1080) })
            {
                Resize(size);
                await Frames();
                foreach (var key in new[] { "portrait", "hotbar1", "menu" }) AssertInside(hud.GetNode<Control>(key), key);
            }
            Check(GameServices.World.RoleTarget == target, "UI 输入穿透地面");
            main.QueueFree();
            await Frames();
            Resize(new Vector2I(640, 360));
            main = await StartMain(directory);
            hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            foreach (var key in new[] { "portrait", "hotbar1", "menu" }) AssertInside(hud.GetNode<Control>(key), "加载边缘设置 " + key);
            GD.Print("UI bounds smoke passed: centered popups, four viewports, fixed close header, scrolling, negative position recovery, HUD drag, saved positions, input isolation.");
            main.QueueFree();
            await Frames();
            main = null;
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree();
            await Frames();
            GetTree().Quit(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>启动并登录隔离角色，不访问正式设置。</summary>
    private async Task<DoubleMainController> StartMain(string directory)
    {
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
        AddChild(main);
        await Frames();
        Press(main, "登录");
        await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Godot.Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Godot.Button.SignalName.Pressed);
        await Frames();
        return main;
    }

    /// <summary>按真实业务入口打开普通面板。</summary>
    private static void Open(Node hud, string title)
    {
        if (title == "系统设置") { Press(hud, "系统"); Press(hud, title); }
        else Press(hud, title);
    }

    /// <summary>触发按钮以覆盖面板生命周期。</summary>
    private static void Press(Node root, string title) => root.FindChildren("*", "Button", true, false).OfType<Button>()
        .Single(button => button.Text == title).EmitSignal(Godot.Button.SignalName.Pressed);

    /// <summary>同时调整窗口和逻辑视口，模拟真实可用空间变化。</summary>
    private void Resize(Vector2I size) { GetWindow().Size = size; GetWindow().ContentScaleSize = size; }

    /// <summary>检查完整控件外框均位于视口内。</summary>
    private void AssertInside(Control control, string label) => Check(GetViewport().GetVisibleRect().Encloses(control.GetGlobalRect()), label + " 超出窗口");

    /// <summary>经真实视口发送鼠标按钮。</summary>
    private void Button(Vector2 point, bool pressed)
    {
        using var input = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = point, Pressed = pressed };
        GetViewport().PushInput(input, true);
    }

    /// <summary>经真实视口发送按住左键的鼠标移动。</summary>
    private void Move(Vector2 point)
    {
        using var input = new InputEventMouseMotion { Position = point, ButtonMask = MouseButtonMask.Left };
        GetViewport().PushInput(input, true);
    }

    /// <summary>点击实际关闭按钮，覆盖 GUI 输入消费。</summary>
    private void Click(Vector2 point) { Button(point, true); Button(point, false); }

    /// <summary>推进网络及多轮容器布局。</summary>
    private async Task Frames()
    {
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>失败以非零退出码报告。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
