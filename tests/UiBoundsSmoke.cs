using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.Const;

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
            // client-ui：头像展示当前角色，等级只在头像角标，状态条采用明确的百分比色阶。
            var portraitPanel = hud.GetNode<PanelContainer>("portrait");
            var avatar = portraitPanel.FindChildren("Avatar", "Control", true, false).OfType<AvatarControl>().Single();
            var name = portraitPanel.FindChildren("PortraitName", "Label", true, false).OfType<Label>().Single();
            var health = portraitPanel.FindChildren("HealthBar", "ProgressBar", true, false).OfType<ProgressBar>().Single();
            var mana = portraitPanel.FindChildren("ManaBar", "ProgressBar", true, false).OfType<ProgressBar>().Single();
            Check(avatar.Level == 5 && avatar.Portrait?.ResourcePath.EndsWith("DThed011_01.png") == true,
                "头像或等级与选中角色不一致");
            Check(name.Text == "晨星" && Math.Abs(health.Value - 86) < 0.01 &&
                Math.Abs(mana.Value - 70) < 0.01, "头像右侧未按角色快照显示名字和比例");
            Check(!portraitPanel.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("经验") || label.Text.Contains("Lv.")), "头像区域出现多余信息");
            Check(GameHud.HealthColor(0.51f) == new Color("3ca66d") &&
                GameHud.HealthColor(0.5f) == new Color("d89137") &&
                GameHud.HealthColor(0.26f) == new Color("d89137") &&
                GameHud.HealthColor(0.25f) == new Color("c94d4d"), "生命百分比边界颜色错误");
            // client-ui：全部创角头型均有当前性别的专用头像或正面头部帧。
            foreach (var gender in new[] { RoleGender.Male, RoleGender.Female })
            foreach (var headId in Enumerable.Range(1, 74))
                Check(GameHud.LoadPortrait(gender, headId) is not null,
                    $"{gender} 头型 {headId} 没有 HUD 头像");
            // client-ui：普通窗口样式不改变聊天、雷达和角色信息的原有边距。
            foreach (var key in new[] { "portrait", "Radar", "Chat" })
            {
                var panel = hud.GetNode<PanelContainer>(key);
                Check(panel.GetThemeStylebox("panel").GetContentMargin(Side.Left) < PopupPanelStyle.ContentInset,
                    key + " 被普通窗口样式改变");
            }
            var target = GameServices.World.RoleTarget;
            // client-ui：首次与再次展开不主动聚焦菜单首项，鼠标悬停继续可用。
            var menuButton = hud.GetNode("menu").FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "系统");
            var menuItems = hud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Move(menuButton.GetGlobalRect().GetCenter());
                Button(menuButton.GetGlobalRect().GetCenter(), true);
                Button(menuButton.GetGlobalRect().GetCenter(), false);
                await Frames();
                Check(hud.GetNode<Control>("SystemMenuOverlay").Visible, "系统菜单未展开");
                Check(!menuItems.Any(button => button.HasFocus()), "打开菜单默认聚焦并高亮了条目");
                Move(menuItems[0].GetGlobalRect().GetCenter());
                await Frames();
                Check(GetViewport().GuiGetHoveredControl() == menuItems[0], "菜单条目无法正常悬停");
                using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
                Check(!menuButton.HasFocus(), "Esc 收起菜单后系统按钮残留焦点高亮");
            }
            Click(menuButton.GetGlobalRect().GetCenter());
            await Frames();
            Click(menuItems[0].GetGlobalRect().GetCenter());
            Move(new Vector2(400, 160));
            await Frames();
            Check(hud.GetNode<Control>("系统设置").Visible && !menuButton.HasFocus(), "打开系统设置后系统按钮残留焦点高亮");
            Press(hud.GetNode<Control>("系统设置"), "关闭");
            foreach (var title in new[] { "角色", "大地图", "GM", "系统设置", "角色设置" })
            {
                Open(hud, title);
                var panel = hud.GetNode<ViewportPanelControl>(title);
                await Frames();
                AssertPopupPadding(panel, title);
                // client-ui：角色窗口仅展示资料，返回角色选择仍由系统菜单负责。
                if (title == "角色")
                    Check(!panel.FindChildren("*", "Button", true, false).OfType<Button>()
                        .Any(button => button.Text == "切换角色"), "角色窗口仍提供切换角色按钮");
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
                    if (size.X == 1280)
                    {
                        Check(!scroll.GetHScrollBar().Visible && !scroll.GetVScrollBar().Visible, title + "在正常窗口出现多余滚动条");
                        Check(scroll.Size.X >= panel.Content.GetCombinedMinimumSize().X + 3 &&
                            scroll.Size.Y >= panel.Content.GetCombinedMinimumSize().Y + 3, title + "未给内容留出缩放余量");
                    }
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

            Resize(new Vector2I(1920, 1080));
            Open(hud, "角色");
            Open(hud, "背包");
            await Frames();
            var rolePanel = hud.GetNode<ViewportPanelControl>("角色");
            var roleScroll = (ScrollContainer)rolePanel.FindChild("PanelContentScroll", true, false);
            var bagPanel = hud.GetNode<BagControl>("背包");
            var bagScroll = bagPanel.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
            var bagGrid = bagPanel.FindChildren("BagGrid", "GridContainer", true, false).OfType<GridContainer>().Single();
            AssertPopupPadding(bagPanel, "背包");
            Check(!roleScroll.GetHScrollBar().Visible && !roleScroll.GetVScrollBar().Visible, "角色面板在完整窗口出现滚动条");
            Check(!bagScroll.GetHScrollBar().Visible && !bagScroll.GetVScrollBar().Visible, "背包在完整窗口出现滚动条");
            Check(bagScroll.Size.X >= bagGrid.GetCombinedMinimumSize().X + 3 &&
                bagScroll.Size.Y >= bagGrid.GetCombinedMinimumSize().Y + 3, "背包网格未留出缩放余量");
            if (OS.GetCmdlineUserArgs().Contains("--capture-ui-bounds") && DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("build/ui-bounds-panels.png");
            }
            rolePanel.Hide();
            bagPanel.Hide();

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
            Open(hud, "角色设置");
            Press(hud, "进入 HUD 调整");
            Press(hud.GetNode<Control>("HudEditWindow"), "关闭");
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
            // client-ui：关闭独立调整窗后 Esc 可再次呼出，保留拖动草稿。
            var editWindow = hud.GetNode<ViewportPanelControl>("HudEditWindow");
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
            Check(editWindow.Visible && !hud.GetNode<Control>("系统设置").Visible, "调整中 Esc 未呼出专用窗口");
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
            Check(editWindow.Visible, "重复 Esc 不应隐藏调整窗");
            // client-ui：完成真实 HUD 拖动后，布局刷新不能把系统菜单立即关闭。
            Press(hud, "完成");
            await Frames();
            Click(systemButton.GetGlobalRect().GetCenter());
            await Frames();
            Check(hud.GetNode<Control>("SystemMenuOverlay").Visible, "保存自定义布局后系统菜单立即消失");
            Resize(new Vector2I(640, 360));
            await Frames();
            Check(hud.GetNode<Control>("SystemMenuOverlay").Visible, "窗口变化错误收起已展开菜单");
            AssertInside(hud.GetNode<Control>("SystemMenuOverlay/SystemMenu"), "布局变化后的系统菜单");
            Click(menuItems[0].GetGlobalRect().GetCenter());
            await Frames();
            Check(hud.GetNode<Control>("系统设置").Visible, "自定义布局后菜单条目无法点击");
            Press(hud.GetNode<Control>("系统设置"), "关闭");
            GameServices.Setting.SetChatSize(160, 900);
            GameServices.Setting.SaveCurrentRole();
            Resize(new Vector2I(1280, 720));
            await Frames();
            Click(systemButton.GetGlobalRect().GetCenter());
            await Frames();
            Check(hud.GetNode<Control>("SystemMenuOverlay").Visible, "超高聊天导致系统菜单无法保持展开");
            Check(hud.GetNode<Control>("Chat").Size.Y == 900, "菜单修复意外改变手动聊天尺寸");
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) GetViewport().PushInput(escape, true);
            GameServices.Setting.SetChatSize(320, 180);
            GameServices.Setting.SaveCurrentRole();
            Press(hud, "进入 HUD 调整");
            await Frames();
            foreach (var key in new[] { "portrait", "hotbar1", "menu" }) GameServices.Setting.SetHudPosition(key, 1, 1);
            GameServices.Setting.SaveCurrentRole();
            foreach (var size in new[] { new Vector2I(480, 270), new Vector2I(1920, 1080) })
            {
                Resize(size);
                await Frames();
                foreach (var key in new[] { "portrait", "hotbar1", "menu" }) AssertInside(hud.GetNode<Control>(key), key);
                AssertInside(editWindow, "调整操作窗口");
                AssertInside(hud.GetNode<Control>("HudEditHint"), "顶部 ESC 提示");
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
        if (title is "系统设置" or "角色设置") { Press(hud, "系统"); Press(hud, title); }
        else Press(hud, title);
    }

    /// <summary>触发按钮以覆盖面板生命周期。</summary>
    private static void Press(Node root, string title) => root.FindChildren("*", "Button", true, false).OfType<Button>()
        .Single(button => button.Text == title).EmitSignal(Godot.Button.SignalName.Pressed);

    /// <summary>同时调整窗口和逻辑视口，模拟真实可用空间变化。</summary>
    private void Resize(Vector2I size) { GetWindow().Size = size; GetWindow().ContentScaleSize = size; }

    /// <summary>检查完整控件外框均位于视口内。</summary>
    private void AssertInside(Control control, string label) => Check(GetViewport().GetVisibleRect().Encloses(control.GetGlobalRect()), label + " 超出窗口");

    /// <summary>验收 client-ui 六个普通窗口的共用四边间距和实际标题栏布局。</summary>
    private static void AssertPopupPadding(PanelContainer panel, string label)
    {
        var style = panel.GetThemeStylebox("panel");
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
            Check(style.GetContentMargin(side) >= PopupPanelStyle.ContentInset, label + " 缺少统一内边距");
        var header = (Control)panel.FindChild("PanelHeader", true, false);
        var outer = panel.GetGlobalRect();
        var title = header.GetGlobalRect();
        Check(title.Position.X >= outer.Position.X + PopupPanelStyle.ContentInset - 1 &&
            title.Position.Y >= outer.Position.Y + PopupPanelStyle.ContentInset - 1 &&
            title.End.X <= outer.End.X - PopupPanelStyle.ContentInset + 1,
            label + " 标题栏仍贴近窗口边缘");
    }

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
