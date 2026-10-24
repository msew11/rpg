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
            CheckSlot(grid, 0, "测试长剑");
            CheckSlot(grid, 1, "测试长剑");
            input.GrabFocus();
            input.Text = "/item 1013";
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
            input.Text = "/item 1014";
            send.EmitSignal(Button.SignalName.Pressed);
            await Frames();
            PressTitle(main, "背包");
            await Frames();
            CheckSlot(grid, 3, "测试盾牌");
            Check(chat.Visible && bag.Visible, "背包与聊天不能同时显示");
            var beforeClick = GameServices.World.RoleTarget;
            Click(grid.GetChild<Control>(0).GetGlobalRect().GetCenter());
            await Frames();
            Check(beforeClick == GameServices.World.RoleTarget, "格子点击穿透地面");

            // 真实悬停等待 Godot 创建提示，同时核对格子的名称契约。
            using (var motion = new InputEventMouseMotion { Position = grid.GetChild<Control>(0).GetGlobalRect().GetCenter() })
                GetViewport().PushInput(motion, true);
            await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
            Check(grid.GetChild<Control>(0).TooltipText == "测试长剑", "悬停名称不正确");
            if (OS.GetCmdlineUserArgs().Contains("--capture-bag") && DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory("build");
                GetViewport().GetTexture().GetImage().SavePng("build/bag-gm.png");
            }
            GetWindow().Size = new Vector2I(640, 360);
            await Frames();
            var viewport = GetViewport().GetVisibleRect();
            Check(viewport.Encloses(bag.GetGlobalRect()) && viewport.Encloses(chat.GetGlobalRect()), "小窗口面板超出视口");
            Check(bag.FindChildren("*", "ScrollContainer", true, false).Any(), "背包缺少滚动区");

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
            PressTitle(main, "背包");
            await Frames();
            var restoredBag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var restoredGrid = (GridContainer)restoredBag.FindChild("BagGrid", true, false);
            CheckSlot(restoredGrid, 3, "测试盾牌");
            foreach (var index in new[] { 0, 2 })
            {
                var slot = restoredGrid.GetChild<Control>(index);
                Check(slot.FindChild("MissingIcon", true, false) is Label && slot.TooltipText.Length > 0, "缺图没有占位或丢失名称");
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

    /// <summary>通过真实节点按钮登录并选择隔离存档角色。</summary>
    private async Task<DoubleMainController> StartMain(string directory, string configDirectory = "res://config")
    {
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"), ConfigDirectory = configDirectory };
        AddChild(main);
        await Frames();
        PressTitle(main, "登录");
        await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
        await Frames();
        return main;
    }

    /// <summary>核对真实格子内纹理、数量与名称，不只检查缓存。</summary>
    private static void CheckSlot(GridContainer grid, int index, string name)
    {
        var slot = grid.GetChild<Control>(index);
        Check(slot.TooltipText == name, "名称提示不匹配");
        Check(slot.FindChild("ItemIcon", true, false) is TextureRect { Texture: not null }, "物品图标未显示");
        Check(slot.FindChild("ItemCount", true, false) is Label { Text: "1" }, "数量一未显示");
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
