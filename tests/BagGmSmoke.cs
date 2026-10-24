using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/role-bag 和 gm-item 的真实 HUD、字节链路、输入隔离与重启展示。</summary>
public partial class BagGmSmoke : Node
{
    /// <summary>用独立存档操作真实登录、聊天和背包，失败以非零退出码报告。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-bag-ui-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            main = await StartMain(directory);
            var bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var chat = main.FindChildren("*", "Control", true, false).OfType<ChatControl>().Single();
            var input = (LineEdit)chat.FindChild("CommandInput", true, false);
            var results = (RichTextLabel)chat.FindChild("ChatResults", true, false);
            var send = (Button)chat.FindChild("SendCommand", true, false);
            var target = GameServices.World.RoleTarget;
            ResizeViewport(new Vector2I(1920, 1080));
            await Frames();
            CheckHudLayout(main, chat);
            await CheckConsoleLogToggle(main);
            await CheckChatHints(chat, input, results);

            input.Text = "  /item 1012 2  ";
            Click(send.GetGlobalRect().GetCenter());
            await Frames();
            Check(GameServices.Session.Bag.Items.Count == 2 && GameServices.Session.Bag.Items.All(item => item.Count == 1), "GM 未发放两个独立装备");
            Check(GameServices.World.RoleTarget == target, "聊天点击穿透地面");
            Check(results.GetParsedText().Contains("已发放") && results.GetParsedText().Contains("测试长剑"), "聊天未回显执行结果");

            PressTitle(main, "背包");
            await Frames();
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            Check(bag.Visible && grid.Columns == 8 && grid.GetChildCount() == 40, "基础背包不是八列四十格");
            CheckBagLayout(bag, grid);
            CheckSlot(grid, 0, "测试长剑");
            CheckSlot(grid, 1, "测试长剑");
            input.GrabFocus();
            SetChatInput(input, "/item 1013 ");
            using (var key = new InputEventKey { Keycode = Key.Enter, Pressed = true }) GetViewport().PushInput(key, true);
            using (var key = new InputEventKey { Keycode = Key.Enter, Pressed = false }) GetViewport().PushInput(key, true);
            await Frames();
            Check(GameServices.Session.Bag.Items.Count == 3 && bag.Visible, "回车未发放或关闭了背包");
            CheckSlot(grid, 2, "测试双手剑");

            var revision = GameServices.Session.Bag.Revision;
            input.Text = "普通聊天";
            send.EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(results.GetParsedText().Contains("聊天暂未开放") && GameServices.Session.Bag.Revision == revision, "普通聊天进入了发放链路");
            input.Text = "/item 1012 40";
            send.EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(results.GetParsedText().Contains("背包空间不足") && GameServices.Session.Bag.Revision == revision, "空间不足未保持整个背包不变");

            PressTitle(bag, "关闭");
            input.GrabFocus();
            SetChatInput(input, "/item 1014 1");
            PushKey(Key.KpEnter);
            await Frames();
            PressTitle(main, "背包");
            await Frames();
            CheckSlot(grid, 3, "测试盾牌");
            Check(chat.Visible && bag.Visible, "背包与聊天不能同时显示");
            ResizeViewport(new Vector2I(1152, 648));
            await Frames();
            CheckHudLayout(main, chat);
            CheckBagLayout(bag, grid);
            var beforeClick = GameServices.World.RoleTarget;
            Click(grid.GetChild<Control>(0).GetGlobalRect().GetCenter());
            await Frames();
            Check(beforeClick == GameServices.World.RoleTarget, "格子点击穿透地面");
            Click(grid.GetChild<Control>(0).GetGlobalRect().GetCenter());
            await Frames();
            Check(GameServices.Session.Bag.Revision == revision + 1, "同格放回修改了背包版本");

            // 真实悬停当次输入即显示名称，不等待默认提示计时器。
            using (var motion = new InputEventMouseMotion { Position = grid.GetChild<Control>(0).GetGlobalRect().GetCenter() })
                GetViewport().PushInput(motion, true);
            CheckName(grid.GetChild<Control>(0), "测试长剑");
            if (OS.GetCmdlineUserArgs().Contains("--capture-bag") && DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory("build");
                GetViewport().GetTexture().GetImage().SavePng("build/bag-gm.png");
            }
            ResizeViewport(new Vector2I(640, 360));
            await Frames();
            CheckHudLayout(main, chat);
            CheckBagLayout(bag, grid);
            var viewport = GetViewport().GetVisibleRect();
            Check(viewport.Encloses(bag.GetGlobalRect()) && viewport.Encloses(chat.GetGlobalRect()), "小窗口面板超出视口");
            Check(bag.FindChildren("*", "ScrollContainer", true, false).Any(), "背包缺少滚动区");

            ResizeViewport(new Vector2I(480, 270));
            await Frames();
            Check(GetViewport().GetVisibleRect().Encloses(bag.GetGlobalRect()), "更小窗口背包超出视口");
            var scroll = bag.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
            Check(scroll.GetVScrollBar().Visible, $"网格超高后未显示滚动条：窗口 {GetWindow().Size}，视口 {GetViewport().GetVisibleRect()}，背包 {bag.Size}，滚动区 {scroll.Size}，网格 {grid.Size}");
            Check(!scroll.GetHScrollBar().Visible, "垂直滚动条挤出了多余的横向滚动条");
            scroll.ScrollVertical = 10000;
            await Frames();
            Check(grid.GetChild<Control>(39).GetGlobalRect().End.Y <= scroll.GetGlobalRect().End.Y + 1, "滚动后无法看到最后一格");

            ResizeViewport(new Vector2I(1280, 720));
            await Frames();
            CheckHudLayout(main, chat);
            CheckBagLayout(bag, grid);
            PressTitle(main, "进入 HUD 调整");
            await Frames();
            CheckHudLayout(main, chat);
            PressTitle(main, "完成 HUD 调整");
            await Frames();
            CheckHudLayout(main, chat);
            GameServices.Setting.SetHudPosition("hotbar1", 0.4f, 0.5f);
            ResizeViewport(new Vector2I(1600, 900));
            await Frames();
            var hotbar = (Control)main.FindChild("hotbar1", true, false);
            Check(hotbar.Position.DistanceTo(GetViewport().GetVisibleRect().Size * new Vector2(0.4f, 0.5f)) < 1, "自定义坐标被默认底边对齐覆盖");
            PressTitle(main, "恢复默认布局");
            await Frames();
            CheckHudLayout(main, chat);

            GameServices.Env.ConsoleLogEnabled = true;
            main.QueueFree();
            await Frames();
            // 重启使用隔离配置，覆盖缺失键和未知图标键的占位回退。
            var configDirectory = Path.Combine(directory, "config");
            Directory.CreateDirectory(configDirectory);
            foreach (var name in new[] { "basic", "map", "item" })
                File.Copy(ProjectSettings.GlobalizePath($"res://config/{name}.json"), Path.Combine(configDirectory, name + ".json"));
            var itemPath = Path.Combine(configDirectory, "item.json");
            var definitions = JsonNode.Parse(File.ReadAllText(itemPath))!.AsArray();
            definitions[0]!.AsObject().Remove("iconAssetId");
            definitions[1]!["iconAssetId"] = "missing/icon";
            File.WriteAllText(itemPath, definitions.ToJsonString());
            main = await StartMain(directory, configDirectory);
            Check(GameServices.Session.Bag.Items.Count == 4, "重启后物品丢失");
            var restartedLog = (CheckButton)main.FindChild("ConsoleLogEnabled", true, false);
            Check(GameServices.Env.ConsoleLogEnabled && restartedLog.ButtonPressed && GameServices.Log.Enabled,
                "重启未从 .env 恢复开启");
            GameServices.Env.ConsoleLogEnabled = false;
            PressTitle(main, "背包");
            await Frames();
            var restoredBag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var restoredGrid = (GridContainer)restoredBag.FindChild("BagGrid", true, false);
            CheckSlot(restoredGrid, 3, "测试盾牌");
            foreach (var index in new[] { 0, 2 })
            {
                var slot = restoredGrid.GetChild<Control>(index);
                Check(slot.FindChild("MissingIcon", true, false) is Label, "缺图没有占位");
                CheckName(slot);
            }
            GD.Print("Bag GM smoke passed: commands, Enter, icons, quantities, names, capacity, reopen, input isolation, small window, restart.");
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

    /// <summary>console-logging：通过真实 GM 控件验证默认值、点击切换、HUD 重建和输入隔离。</summary>
    private async Task CheckConsoleLogToggle(Node main)
    {
        var toggle = (CheckButton)main.FindChild("ConsoleLogEnabled", true, false);
        Check(!toggle.ButtonPressed && !GameServices.Env.ConsoleLogEnabled && !GameServices.Log.Enabled,
            "日志未默认关闭");
        PressTitle(main, "GM");
        await Frames();
        var target = GameServices.World.RoleTarget;
        Click(toggle.GetGlobalRect().GetCenter());
        await Frames();
        Check(toggle.ButtonPressed && GameServices.Env.ConsoleLogEnabled && GameServices.Log.Enabled,
            "GM 日志开关没有立即开启");
        Check(target == GameServices.World.RoleTarget, "日志开关点击穿透世界");
        var envPath = GameServices.Env.EnvFilePath!;
        Check(File.ReadAllText(envPath).Contains("RPG_CONSOLE_LOG_ENABLED=true"), "GM 未保存 .env");
        var rebuilt = new GameHud { Visible = false };
        AddChild(rebuilt);
        await Frames();
        Check(((CheckButton)rebuilt.FindChild("ConsoleLogEnabled", true, false)).ButtonPressed,
            "重建 HUD 未保留环境日志标记");
        rebuilt.QueueFree();
        await Frames();
        Click(toggle.GetGlobalRect().GetCenter());
        await Frames();
        Check(!GameServices.Env.ConsoleLogEnabled && !GameServices.Log.Enabled, "日志没有立即关闭");
        Check(File.ReadAllText(envPath).Contains("RPG_CONSOLE_LOG_ENABLED=false"), "关闭未保存 .env");
        using (var locked = new FileStream(envPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))
        {
            Click(toggle.GetGlobalRect().GetCenter());
            await Frames();
            Check(!toggle.ButtonPressed && !GameServices.Env.ConsoleLogEnabled &&
                ((Label)main.FindChild("NetworkError", true, false)).Text.Contains("无法保存 .env"),
                ".env 保存失败未恢复控件或显示原因");
        }
        PressTitle(main, "GM");
        await Frames();
    }

    /// <summary>验收 gm-item 上下文提示：真实点击与按键补全、格式颜色、灰字隔离和视口边界。</summary>
    private async Task CheckChatHints(ChatControl chat, LineEdit input, RichTextLabel results)
    {
        var popup = (PanelContainer)chat.FindChild("CommandSuggestions", true, false);
        var candidates = (ItemList)chat.FindChild("CommandCandidates", true, false);
        var hint = (Label)chat.FindChild("QuantityHint", true, false);
        Check(input.PlaceholderText == "" && results.GetParsedText() == "", "仍有固定聊天提示");
        input.GrabFocus();
        PushKey(Key.Slash, '/');
        await Frames();
        Check(input.Text == "/" && popup.Visible && candidates.ItemCount == 1 && candidates.GetItemText(0).Contains("/item"), "斜杠未显示命令");
        await CaptureChat("commands");
        PushKey(Key.Tab);
        await Frames();
        Check(input.Text == "/item " && input.HasFocus(), "Tab 未补全或焦点丢失");
        Check(candidates.ItemCount == GameServices.Config.Item.Items.Count, "可发放道具列表不完整");
        await CaptureChat("items");
        foreach (var item in GameServices.Config.Item.Items.Values)
            Check(Enumerable.Range(0, candidates.ItemCount).Any(index => candidates.GetItemText(index) == $"{item.Id}  {item.Name}"), "道具 ID 或名称不匹配");
        var target = GameServices.World.RoleTarget;
        var revision = GameServices.Session.Bag.Revision;
        var itemRect = candidates.GetItemRect(0);
        Click(candidates.GlobalPosition + itemRect.GetCenter());
        await Frames();
        Check(input.Text == "/item 1012 " && input.HasFocus() && !popup.Visible, "鼠标补全道具失败");
        Check(hint.Visible && hint.Text == "数量" && !input.Text.Contains("数量"), "数量灰字未显示或污染输入");
        Check(hint.GetThemeColor("font_color").R == hint.GetThemeColor("font_color").G, "数量提示不是灰色");
        Check(!input.HasThemeColorOverride("font_color"), "未显式输入数量就变绿");
        await CaptureChat("quantity");
        PushKey(Key.Key2, '2');
        await Frames();
        var validColor = input.GetThemeColor("font_color");
        Check(input.Text == "/item 1012 2" && validColor.G > validColor.R && validColor.G > validColor.B && !hint.Visible, "有效命令未整行变绿或灰字未消失");
        await CaptureChat("valid");
        foreach (var text in new[] { "/item 1012 0", "/item 1012 -1", "/item 1012 +1", "/item 1012 abc",
            "/item 1012 2147483648", "/item 9999 1", "/unknown 1012 1", "/item 1012 1 extra", "普通文字" })
        {
            SetChatInput(input, text);
            Check(!input.HasThemeColorOverride("font_color") && !hint.Visible && !popup.Visible, "非法输入保留提示或绿色：" + text);
        }
        SetChatInput(input, " /item\t1012\t2 ");
        Check(input.HasThemeColorOverride("font_color"), "合法首尾空白与分隔符校验不一致");
        SetChatInput(input, "/item 1012");
        Check(popup.Visible && candidates.ItemCount == 1, "ID 前缀未筛选");
        PushKey(Key.Escape);
        Check(!popup.Visible, "Escape 未隐藏候选");
        SetChatInput(input, "/item ");
        PushKey(Key.Down);
        PushKey(Key.Tab);
        Check(input.Text == "/item 1013 ", "上下键候选选择无效");
        input.ReleaseFocus();
        Check(!popup.Visible && !hint.Visible, "失焦后提示未隐藏");
        input.GrabFocus();
        // 两种回车都必须只确认当前阶段，并使用实际选中项而非固定第一项。
        foreach (var enter in new[] { Key.Enter, Key.KpEnter })
        {
            SetChatInput(input, "/");
            var history = results.GetParsedText();
            PushKey(enter);
            await Frames();
            Check(input.Text == "/item " && popup.Visible && input.HasFocus(), "回车未确认命令候选：" + enter);
            PushKey(Key.Down);
            PushKey(enter);
            await Frames();
            Check(input.Text == "/item 1013 " && !popup.Visible && hint.Visible && input.HasFocus(), "回车未填入选中的道具：" + enter);
            Check(results.GetParsedText() == history && GameServices.Session.Bag.Revision == revision,
                "回车补全误发送命令：" + enter);
        }
        SetChatInput(input, "/item ");
        ResizeViewport(new Vector2I(480, 270));
        await Frames();
        await CaptureChat("small");
        Check(popup.Visible && GetViewport().GetVisibleRect().Encloses(popup.GetGlobalRect()),
            $"小窗口候选越界：显示 {popup.Visible}，视口 {GetViewport().GetVisibleRect()}，候选 {popup.GetGlobalRect()}，输入 {input.GetGlobalRect()}");
        Check(popup.GetGlobalRect().End.Y <= input.GetGlobalRect().Position.Y, "候选遮挡输入框");
        candidates.GetVScrollBar().Value = candidates.GetVScrollBar().MaxValue;
        await Frames();
        Check(candidates.GetItemRect(candidates.ItemCount - 1).End.Y <= candidates.Size.Y, "候选滚动后无法看到末项");
        SetChatInput(input, "");
        Check(!popup.Visible && !hint.Visible && !input.HasThemeColorOverride("font_color"), "清空未复位提示状态");
        Check(GameServices.World.RoleTarget == target && GameServices.Session.Bag.Revision == revision, "候选操作穿透世界或提交命令");
        ResizeViewport(new Vector2I(1920, 1080));
        await Frames();
    }

    /// <summary>可选保存四个真实输入阶段，便于 Windows 图形模式复核。</summary>
    private async Task CaptureChat(string stage)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-chat") || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Directory.CreateDirectory("build");
        GetViewport().GetTexture().GetImage().SavePng($"build/chat-{stage}.png");
    }

    /// <summary>程序设置文本后模拟编辑通知，光标放在末尾以核对数量提示。</summary>
    private static void SetChatInput(LineEdit input, string text)
    {
        input.Text = text;
        input.CaretColumn = text.Length;
        input.EmitSignal(LineEdit.SignalName.TextChanged, text);
    }

    /// <summary>通过实际视口分发完整按键，覆盖 GUI 焦点与输入消费。</summary>
    private void PushKey(Key key, char unicode = '\0')
    {
        using var press = new InputEventKey { Keycode = key, Unicode = unicode, Pressed = true };
        using var release = new InputEventKey { Keycode = key, Unicode = unicode, Pressed = false };
        GetViewport().PushInput(press, true);
        GetViewport().PushInput(release, true);
    }

    /// <summary>通过真实节点按钮登录并选择隔离存档角色。</summary>
    private async Task<DoubleMainController> StartMain(string directory, string configDirectory = "res://config")
    {
        var main = new DoubleMainController
        {
            SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"),
            ConfigDirectory = configDirectory,
            EnvFilePath = Path.Combine(directory, ".env")
        };
        AddChild(main);
        await Frames();
        PressTitle(main, "登录");
        await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
        await Frames();
        return main;
    }

    /// <summary>真实悬停立即显示普通文本名称，缺图时同样生效。</summary>
    private static void CheckName(Control slot, string? expected = null)
    {
        Node owner = slot;
        while (owner is not BagControl) owner = owner.GetParent();
        using var motion = new InputEventMouseMotion { Position = slot.GetGlobalRect().GetCenter() };
        slot.GetViewport().PushInput(motion, true);
        var popup = (Label)owner.FindChild("ItemNamePopup", true, false);
        Check(popup.Visible && popup.Text.Length > 0 && (expected is null || popup.Text == expected), "名称未即时显示或不匹配");
    }

    /// <summary>核对真实格子内纹理、数量与名称，不只检查缓存。</summary>
    private static void CheckSlot(GridContainer grid, int index, string name)
    {
        var slot = grid.GetChild<Control>(index);
        CheckName(slot, name);
        Check(slot.FindChild("ItemIcon", true, false) is TextureRect { Texture: not null }, "物品图标未显示");
        Check(slot.FindChild("ItemCount", true, false) is Label { Text: "1" }, "数量一未显示");
    }

    /// <summary>验收 client-ui 内容贴合与底部对齐：真实外框底边一致，内容下方没有空带。</summary>
    private void CheckHudLayout(Node main, ChatControl chat)
    {
        var bottom = GetViewport().GetVisibleRect().End.Y - 16;
        foreach (var key in new[] { "hotbar1", "menu" })
        {
            var panel = (PanelContainer)main.FindChild(key, true, false);
            Check(Mathf.Abs(panel.GetGlobalRect().End.Y - bottom) <= 1, key + " 未与聊天底边对齐");
        }
        Check(Mathf.Abs(chat.GetGlobalRect().End.Y - bottom) <= 1, "聊天底边未贴齐");
        if (GetViewport().GetVisibleRect().Size.X >= 1152)
        {
            var bar = (Control)main.FindChild("hotbar1", true, false);
            var menu = (Control)main.FindChild("menu", true, false);
            Check(chat.GetGlobalRect().End.X < bar.GetGlobalRect().Position.X, "默认聊天遮挡技能栏");
            Check(bar.GetGlobalRect().End.X < menu.GetGlobalRect().Position.X, "默认菜单遮挡技能栏");
        }
        foreach (var key in new[] { "portrait", "hotbar1", "menu" })
        {
            var panel = (PanelContainer)main.FindChild(key, true, false);
            var content = panel.GetChild<VBoxContainer>(0).GetChildren().OfType<Control>().Last(control => control.Visible);
            var gap = panel.GetGlobalRect().End.Y - content.GetGlobalRect().End.Y;
            Check(gap <= panel.GetThemeStylebox("panel").GetContentMargin(Side.Bottom) + 1, key + " 内容下方仍有空带");
        }
    }

    /// <summary>验收 client-ui 背包完整展示：完整网格右侧和底部仅有主题边距。</summary>
    private static void CheckBagLayout(BagControl bag, GridContainer grid)
    {
        var gap = bag.GetGlobalRect().End - grid.GetGlobalRect().End;
        var style = bag.GetThemeStylebox("panel");
        Check(Mathf.Abs(gap.X - style.GetContentMargin(Side.Right)) <= 1, "背包右侧仍有空白");
        Check(Mathf.Abs(gap.Y - style.GetContentMargin(Side.Bottom)) <= 1, "背包底部仍有空白");
        var scroll = (ScrollContainer)grid.GetParent();
        Check(!scroll.GetVScrollBar().Visible && !scroll.GetHScrollBar().Visible, "完整网格不应显示滚动条");
    }

    /// <summary>按可见标题操作实际按钮。</summary>
    private static void PressTitle(Node root, string title) => root.FindChildren("*", "Button", true, false)
        .OfType<Button>().Single(button => button.Text == title).EmitSignal(Button.SignalName.Pressed);

    /// <summary>从视口注入完整点击，检查真实 GUI 消费。</summary>
    private void Click(Vector2 position)
    {
        using var press = new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true };
        using var release = new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false };
        GetViewport().PushInput(press, true);
        GetViewport().PushInput(release, true);
    }

    /// <summary>同时设置窗口与逻辑视口，确保 headless 也真实覆盖不同布局尺寸。</summary>
    private void ResizeViewport(Vector2I size)
    {
        GetWindow().Size = size;
        GetWindow().ContentScaleSize = size;
    }

    /// <summary>等待网络队列、GUI 布局和延迟释放。</summary>
    private async Task Frames()
    {
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>以异常中止失败场景。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
