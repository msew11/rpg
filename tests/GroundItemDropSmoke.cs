using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/ground-item-drop 的真实地图点击、数量确认、地面表现和重启恢复。</summary>
public partial class GroundItemDropSmoke : Node
{
    /// <summary>通过隔离配置及真实界面验证两侧同步与表现生命周期。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-ground-drop-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var config = Path.Combine(directory, "config");
            var server = Path.Combine(directory, "server");
            Directory.CreateDirectory(config);
            foreach (var name in new[] { "basic", "map", "item" })
                File.Copy(ProjectSettings.GlobalizePath($"res://config/{name}.json"), Path.Combine(config, name + ".json"));
            var items = JsonNode.Parse(File.ReadAllText(Path.Combine(config, "item.json")))!.AsArray();
            items.Add(new JsonObject { ["id"] = 2000, ["name"] = "验收堆叠", ["category"] = 0, ["subCategory"] = 0, ["maxStack"] = 10 });
            File.WriteAllText(Path.Combine(config, "item.json"), items.ToJsonString());
            RoleTestData.Seed(server);
            var repo = new RepoServerRole(server);
            repo.Save(repo.Load(1001) with { Bag = new() { NextInstanceId = 3,
                Items = [new(1, 2000, 0, 5), new(2, 1012, 1, 1)] } });
            main = await StartMain(directory, config);
            var bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            var dialog = (ConfirmationDialog)bag.FindChild("DropItemDialog", true, false);
            var count = (SpinBox)dialog.FindChild("DropItemCount", true, false);
            var originalTarget = GameServices.World.RoleTarget;
            var original = GameServices.Session.Bag;
            await ChooseGroundDrop(grid.GetChild<Control>(0));
            Check(dialog.Visible && count.Value == 5 && count.MinValue == 1 && count.MaxValue == 5, "数量确认未默认整叠或上下限错误");
            dialog.GetCancelButton().EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(ReferenceEquals(original, GameServices.Session.Bag) && GameServices.Session.Map!.Objects.Count == 0, "取消修改了物品");

            await ChooseGroundDrop(grid.GetChild<Control>(0));
            count.GetLineEdit().Text = "2";
            dialog.GetOkButton().EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(GameServices.Session.Bag.Items.Single(item => item.InstanceId == 1).Count == 3, "部分丢弃未保留源数量");
            var ground = GameServices.Session.Map!.Objects.Values.Single();
            Check(ground.Count == 2 && ground.ItemId == 2000, "AOI 未接纳地面道具");
            var view = main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Single();
            Check(view.GetChildCount() == 1 && ((Label)view.FindChild("GroundItemName", true, false)).Text == "验收堆叠 ×2", "地面名称数量错误");
            Check(view.FindChild("GroundItemIcon", true, false) is TextureRect { Texture: not null }, "缺图没有占位");
            Check(GameServices.World.RoleTarget == originalTarget, "丢弃点击触发世界走路");

            // 确认期间收到新背包，必须取消旧上下文。
            await ChooseGroundDrop(grid.GetChild<Control>(0));
            GameServices.Session.SendGm("item 2000 1");
            await Frames();
            Check(!dialog.Visible, "背包刷新未使确认失效");
            var revision = GameServices.Session.Bag.Revision;
            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            await Frames();
            Check(GameServices.Session.Bag.Revision == revision && GameServices.Session.Map.Objects.Count == 1, "旧确认扣除了新背包");

            await ChooseGroundDrop(grid.GetChild<Control>(1));
            Check(!dialog.Visible, "单件丢弃仍弹出数量窗");
            await Frames();
            Check(GameServices.Session.Bag.Items.All(item => item.InstanceId != 2), "全部丢弃未移除源");
            Check(view.GetChildCount() == 2 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "配置图标未显示");

            Check(view.GetChildren().OfType<Node2D>().Select(child => child.Position).Distinct().Count() == 2, "同落点表现完全重叠");

            // 数量窗打开时关闭背包，不允许继续确认。
            await ChooseGroundDrop(grid.GetChild<Control>(0));
            bag.Hide();
            await Frames();
            Check(!dialog.Visible, "关闭背包未关闭数量确认");
            Check(GameServices.Session.Bag.Items.Single().Count == 4, "关闭数量窗修改背包");
            GameServices.Net.Tick(5);
            var saved = repo.Load(1001).Bag;
            main.QueueFree(); await Frames(); main = null;
            main = await StartMain(directory, config);
            Check(GameServices.Session.Bag.Revision == saved.Revision && GameServices.Session.Bag.Items.Select(item => (item.InstanceId, item.Count)).SequenceEqual(saved.Items.Select(item => (item.InstanceId, item.Count))), "重启后背包未恢复");
            Check(GameServices.Session.Map!.Objects.Count == 2, "重启后地图未恢复");
            view = main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Single();
            Check(view.GetChildCount() == 2, "重启后地面表现未恢复");
            GameServices.Session.SubmitMove(1, new Vector2(23000, 23000));
            GameServices.Net.Tick(100);
            await Frames();
            Check(GameServices.Session.Map.Objects.Count == 0 && view.GetChildCount() == 0, "离开 AOI 未移除地面表现");
            GameServices.Session.SubmitMove(2, new Vector2(ground.X, ground.Y));
            GameServices.Net.Tick(100);
            await Frames();
            Check(view.GetChildCount() == 2, "再次进入 AOI 未恢复表现");
            PressTitle(main, "系统"); PressTitle(main, "返回角色选择"); await Frames();
            Check(!main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Any(), "离开世界未释放表现");
            GD.Print("Ground item drop smoke passed: quantity, cancel, partial/full, stale confirmation, UI input isolation, icons, AOI, restart.");
            main.QueueFree(); await Frames(); main = null;
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree(); await Frames(); main = null;
            GetTree().Quit(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>真实左键拿起后点击地图，不直接调用业务处理方法。</summary>
    private async Task ChooseGroundDrop(Control slot)
    {
        var point = slot.GetGlobalRect().GetCenter();
        MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
        point = new Vector2(20, 220);
        MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
    }

    /// <summary>进入真实角色和背包，正式物品配置仍由隔离文件提供。</summary>
    private async Task<DoubleMainController> StartMain(string directory, string config)
    {
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"), ConfigDirectory = config };
        AddChild(main);
        await Frames();
        PressTitle(main, "登录");
        await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
        await Frames();
        PressTitle(main, "背包");
        await Frames();
        return main;
    }

    /// <summary>注入视口鼠标按下或释放，验证 GUI 消费与世界输入隔离。</summary>
    private void MouseButtonAt(Vector2 point, bool pressed)
    {
        using var button = new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
        GetViewport().PushInput(button, true);
    }

    /// <summary>通过实际按钮进入或打开面板。</summary>
    private static void PressTitle(Node root, string title) => root.FindChildren("*", "Button", true, false).OfType<Button>()
        .Single(button => button.Text == title).EmitSignal(Button.SignalName.Pressed);

    /// <summary>等待网络收发、拖放状态和 GUI 布局。</summary>
    private async Task Frames()
    {
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>将验收失败转换为场景非零退出。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
