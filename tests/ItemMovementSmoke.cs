using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/role-bag 的真实两次点击、合并、交换、取消、过期保护与重启。</summary>
public partial class ItemMovementSmoke : Node
{
    /// <summary>使用隔离物品配置和角色存档，鼠标事件必须经过真实视口。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-item-drag-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var config = Path.Combine(directory, "config");
            var server = Path.Combine(directory, "server");
            Directory.CreateDirectory(config);
            foreach (var name in new[] { "basic", "map", "item", "equipment" })
                File.Copy(ProjectSettings.GlobalizePath($"res://config/{name}.json"), Path.Combine(config, name + ".json"));
            var definitions = JsonNode.Parse(File.ReadAllText(Path.Combine(config, "item.json")))!.AsArray();
            definitions.Add(new JsonObject { ["id"] = 2000, ["name"] = "隔离堆叠道具", ["category"] = 1,
                ["subCategory"] = 2, ["relatedId"] = 1012, ["maxStack"] = 10 });
            File.WriteAllText(Path.Combine(config, "item.json"), definitions.ToJsonString());
            RoleTestData.Seed(server);
            var repo = new RepoRole(server);
            RoleTestData.SetItems(server, 1001, [new(1, 1012, 0, 1), new(2, 2000, 1, 8), new(3, 2000, 2, 3), new(4, 2000, 3, 2)], 5);
            main = await StartMain(directory, config);
            var bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            var worldTarget = GameServices.World.RoleTarget;

            await Drag(grid.GetChild<Control>(0), grid.GetChild<Control>(7));
            Check(GameServices.Session.Bag.Items.Single(item => item.InstanceId == 1).SlotIndex == 7, "空格移动失败");
            Check(grid.GetChild<Control>(7).FindChild("ItemIcon", true, false) is not null && grid.GetChild<Control>(0).GetChildCount() == 0, "格子显示未随移动刷新");
            await Drag(grid.GetChild<Control>(7), grid.GetChild<Control>(2));
            Check(GameServices.Session.Bag.Items.Single(item => item.InstanceId == 1).SlotIndex == 2 &&
                GameServices.Session.Bag.Items.Single(item => item.InstanceId == 3).SlotIndex == 7, "占用格交换失败");
            await Drag(grid.GetChild<Control>(1), grid.GetChild<Control>(7));
            Check(GameServices.Session.Bag.Items.Single(item => item.InstanceId == 2).Count == 1 &&
                GameServices.Session.Bag.Items.Single(item => item.InstanceId == 3).Count == 10, "部分合并未保留原格剩余数量");
            await Drag(grid.GetChild<Control>(1), grid.GetChild<Control>(3));
            Check(GameServices.Session.Bag.Items.All(item => item.InstanceId != 2) &&
                GameServices.Session.Bag.Items.Single(item => item.InstanceId == 4).Count == 3, "完全合并未清除耗尽实例");
            Check(grid.GetChild<Control>(3).FindChild("ItemCount", true, false) is Label { Text: "3" }, "合并数量未更新");

            var revision = GameServices.Session.Bag.Revision;
            await Drag(grid.GetChild<Control>(2), grid.GetChild<Control>(2));
            await BeginDrag(grid.GetChild<Control>(7));
            await EndDrag(new Vector2(20, 220));
            var dropDialog = (ConfirmationDialog)bag.FindChild("DropItemDialog", true, false);
            Check(dropDialog.Visible, "点击地图没有打开丢弃数量确认");
            dropDialog.GetCancelButton().EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(GameServices.Session.Bag.Revision == revision, "原格或背包外放下修改了背包");
            await BeginDrag(grid.GetChild<Control>(0), false);
            await EndDrag(new Vector2(20, 220));
            Check(GameServices.Session.Bag.Revision == revision, "空格开始了物品移动");

            await BeginDrag(grid.GetChild<Control>(2));
            GameServices.Session.SendGm("item 1013");
            await Frames();
            revision = GameServices.Session.Bag.Revision;
            await EndDrag(grid.GetChild<Control>(5).GetGlobalRect().GetCenter());
            Check(GameServices.Session.Bag.Revision == revision && GameServices.Session.Bag.Items.Single(item => item.InstanceId == 1).SlotIndex == 2,
                "背包更新后旧拖放仍然执行");

            await BeginDrag(grid.GetChild<Control>(2));
            bag.Hide();
            await Frames();
            await EndDrag(new Vector2(20, 220));
            PressTitle(main, "背包");
            await Frames();
            Check(GameServices.Session.Bag.Revision == revision, "关闭面板仍然提交拖放");

            // 滚动时保留点选移动，把物品放到当前屏幕外的最后一格。
            GetWindow().Size = new Vector2I(480, 270);
            GetWindow().ContentScaleSize = new Vector2I(480, 270);
            await Frames();
            await BeginDrag(grid.GetChild<Control>(7));
            var scroll = bag.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
            scroll.EnsureControlVisible(grid.GetChild<Control>(39));
            await Frames();
            Check(scroll.ScrollVertical > 0, "小窗口未实际滚动到末格");
            await EndDrag(grid.GetChild<Control>(39).GetGlobalRect().GetCenter());
            Check(GameServices.Session.Bag.Items.Single(item => item.InstanceId == 3).SlotIndex == 39, "末格拖放失败");
            Check(GameServices.World.RoleTarget == worldTarget, "拖放输入穿透到世界移动");

            GetWindow().Size = new Vector2I(1280, 720);
            GetWindow().ContentScaleSize = new Vector2I(1280, 720);
            await Frames();

            // server-write-back：先推进到落地节点，再核对失败请求与重启文件。
            await GameServices.MockServer.FlushAsync();
            var saved = (await repo.LoadAsync(1001));
            GameServices.Session.MoveItem(99999, 4);
            await Frames();
            Check(bag.FindChild("ItemMoveStatus", true, false) is Label { Visible: true, Text: "源物品不存在" }, "失败响应没有提示");
            Check((await repo.LoadAsync(1001)).Items.SequenceEqual(saved.Items), "失败请求修改了存档");
            await BeginDrag(grid.GetChild<Control>(2));
            PressTitle(main, "系统");
            PressTitle(main, "返回角色选择");
            await Frames();
            ((Button)main.FindChild("SelectRole1002", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            PressTitle(main, "背包");
            await Frames();
            var otherBag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            await EndDrag(((GridContainer)otherBag.FindChild("BagGrid", true, false)).GetChild<Control>(0).GetGlobalRect().GetCenter());
            Check(GameServices.Session.RoleId == 1002 && GameServices.Session.Bag.Items.Count == 0 && (await repo.LoadAsync(1001)).Items.SequenceEqual(saved.Items),
                "切角后的旧拖放影响了角色背包");
            main.QueueFree();
            await Frames();
            main = await StartMain(directory, config);
            Check(GameServices.Session.Bag.Items.Select(item => (item.InstanceId, item.SlotIndex, item.Count))
                .SequenceEqual(saved.Items.Select(item => (item.InstanceId, item.SlotIndex, item.Count))), "重启未恢复移动及合并结果");
            GD.Print("Item movement smoke passed: click pickup, release retention, centered preview, instant name, move, swap, partial/full merge, cancel, stale drag, last slot, errors, world input, restart.");
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

    /// <summary>真实按下立即拿起，松开及无按键移动后仍保持，图标中心跟随指尖。</summary>
    private async Task BeginDrag(Control source, bool expectDrag = true)
    {
        var point = source.GetGlobalRect().GetCenter();
        using var hover = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(hover, true);
        var bag = source.GetParent().GetParent().GetParent().GetParent().GetParent();
        var name = (Label)bag.FindChild("ItemNamePopup", true, false);
        if (expectDrag) Check(name.Visible && name.Text.Length > 0, "指向物品后未立即显示名称");
        MouseButtonAt(point, true);
        var cursor = FindChildren("*", "Node", true, false).OfType<PointerCursor>().Single();
        Check(cursor.IsPressed, "格子消费输入后鼠标未显示按下状态");
        await Frames();
        Check(Preview() is not null == expectDrag, "按下没有立即拿起或空格错误拿起");
        MouseButtonAt(point, false);
        Check(!cursor.IsPressed, "拿起物品释放后鼠标未恢复普通状态");
        await Frames();
        Check(Preview() is not null == expectDrag, "释放后没有保持拿起状态");
        using var motion = new InputEventMouseMotion { Position = point + new Vector2(18, 0), GlobalPosition = point + new Vector2(18, 0),
            Relative = new Vector2(18, 0) };
        GetViewport().PushInput(motion, true);
        await Frames();
        if (expectDrag)
        {
            Check(!name.Visible, "拿起时仍显示名称");
            Check(Preview()!.GetGlobalRect().GetCenter().IsEqualApprox(point + new Vector2(18, 0)), "物品图标中心未对齐鼠标指尖");
        }
    }

    /// <summary>松开移动至目标不提交，再次按下才放置；取消后释放不产生新点击。</summary>
    private async Task EndDrag(Vector2 target)
    {
        var before = GameServices.Session.Bag;
        using var motion = new InputEventMouseMotion { Position = target, GlobalPosition = target };
        GetViewport().PushInput(motion, true);
        await Frames();
        Check(ReferenceEquals(before, GameServices.Session.Bag), "仅移动鼠标就提交了物品");
        if (Preview() is not null) MouseButtonAt(target, true);
        MouseButtonAt(target, false);
        await Frames();
        Check(Preview() is null, "放下后拿起状态未结束");
    }

    /// <summary>取得屏幕物品预览，验证真实显示而非调用处理方法。</summary>
    private Control? Preview() => FindChildren("PickedItemPreview", "Control", true, false).OfType<Control>().SingleOrDefault();

    /// <summary>执行完整点选移动，服务器处理前不得乐观修改缓存。</summary>
    private async Task Drag(Control source, Control target)
    {
        var before = GameServices.Session.Bag;
        await BeginDrag(source);
        Check(ReferenceEquals(before, GameServices.Session.Bag), "开始拖动提前修改了权威缓存");
        await EndDrag(target.GetGlobalRect().GetCenter());
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
        await SmokeNetwork.WaitForAccount(this);
        for (var index = 0; index < 4; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>将验收失败转换为场景非零退出。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
