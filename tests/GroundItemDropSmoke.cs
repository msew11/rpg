using System;
using System.Collections.Generic;
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
using Rpg.World.Utils;
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
            foreach (var name in new[] { "basic", "map", "item", "equipment" })
                File.Copy(ProjectSettings.GlobalizePath($"res://config/{name}.json"), Path.Combine(config, name + ".json"));
            var items = JsonNode.Parse(File.ReadAllText(Path.Combine(config, "item.json")))!.AsArray();
            items.Add(new JsonObject { ["id"] = 2000, ["name"] = "验收堆叠", ["category"] = 1, ["subCategory"] = 2, ["relatedId"] = 1012, ["maxStack"] = 10 });
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
            var overlay = main.FindChildren("*", "Node", true, false).OfType<GroundItemOverlayControl>().Single();
            // ground-item-drop：图标下不再常驻显示数量，信息框默认隐藏。
            var firstInfo = overlay.FindChildren("*", "PanelContainer", true, false).OfType<GroundItemInfoControl>().Single();
            Check(view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 1 &&
                view.FindChild("GroundItemCount", true, false) is null && !firstInfo.Visible,
                "地面仍常驻数量或信息框未默认隐藏");
            SendKey(Key.Z); await Frames();
            Check(firstInfo.Visible && firstInfo.GetNode<Label>("InfoText").Text == "验收堆叠 ×2" &&
                firstInfo.GetParent<CanvasLayer>().Layer > 0, "Z 未在 UI 层显示堆叠信息");
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

            // 对应 Ctrl 单件销毁确认与取消：松开修饰键后仍保留销毁语义。
            var beforeDestroy = GameServices.Session.Bag;
            await ChooseGroundDrop(grid.GetChild<Control>(1), true);
            Check(dialog.Visible && dialog.Title == "销毁道具" && dialog.GetOkButton().Text == "销毁", "单件 Ctrl 未要求销毁确认");
            Check(((Label)dialog.FindChild("DropItemPrompt", true, false)).Text.Contains("永久删除"), "缺少永久销毁说明");
            Check(ReferenceEquals(beforeDestroy, GameServices.Session.Bag), "确认前已扣除物品");
            dialog.GetCancelButton().EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(ReferenceEquals(beforeDestroy, GameServices.Session.Bag) && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 1, "取消销毁改变了状态");

            await ChooseGroundDrop(grid.GetChild<Control>(1));
            Check(!dialog.Visible, "单件丢弃仍弹出数量窗");
            await Frames();
            Check(GameServices.Session.Bag.Items.All(item => item.InstanceId != 2), "全部丢弃未移除源");
            Check(view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "配置图标未显示");
            var infoViews = overlay.FindChildren("*", "PanelContainer", true, false).OfType<GroundItemInfoControl>().ToArray();
            Check(infoViews.Length == 2 && infoViews.All(info => info.Visible) &&
                infoViews.Any(info => info.GetNode<Label>("InfoText").Text == "测试长剑") &&
                infoViews.All(info => !info.GetNode<Label>("InfoText").Text.Contains("×1")) &&
                !infoViews[0].GetGlobalRect().Intersects(infoViews[1].GetGlobalRect()),
                "单件信息仍显示 ×1 或信息框重叠");
            // ground-item-drop：邻近物品先沿中心列以小间距堆叠，悬停不改变紧凑尺寸。
            Check(infoViews.All(info => info.GetNode<Label>("InfoText").GetThemeFontSize("font_size") == 16 &&
                info.GetCombinedMinimumSize().Y <= 30), "地面物品信息框仍然过大");
            var normalSize = infoViews[0].GetCombinedMinimumSize();
            infoViews[0].SetHighlighted(true);
            Check(infoViews[0].GetCombinedMinimumSize() == normalSize, "信息框高亮改变了排布尺寸");
            infoViews[0].SetHighlighted(false);
            var placed = new List<Rect2>();
            var layoutAnchor = new Vector2(150, 150);
            var layoutSize = new Vector2(80, 25);
            for (var index = 0; index < 3; index++)
            {
                var rect = GroundItemOverlayControl.FindInfoPlacement(layoutAnchor, layoutSize, new Vector2(300, 250), placed);
                placed.Add(rect ?? throw new InvalidOperationException("邻近信息框没有可用位置"));
            }
            Check(placed.All(rect => Mathf.IsEqualApprox(rect.Position.X, placed[0].Position.X)) &&
                Mathf.IsEqualApprox(placed[0].Position.Y - placed[1].Position.Y, layoutSize.Y + 2) &&
                Mathf.IsEqualApprox(placed[1].Position.Y - placed[2].Position.Y, layoutSize.Y + 2),
                "邻近信息框未优先紧凑纵向堆叠");
            SendKey(Key.Z); await Frames();
            Check(infoViews.All(info => !info.Visible), "再次按 Z 未隐藏地面信息");

            // client-actor-presentation、ground-item-pickup：同点实体保持真实坐标，图标仍错开并参与 Y 排序。
            var worldVisual = main.GetChildren().OfType<Node2D>().Single(node => node.Name == "WorldVisual");
            var actorView = worldVisual.GetChildren().OfType<ActorView>().Single();
            var itemNodes = view.GetChildren().OfType<Node2D>().ToArray();
            Check(worldVisual.YSortEnabled && view.YSortEnabled && actorView.ZIndex == view.ZIndex,
                "角色与地面物品未共用 Y 排序层");
            Check(itemNodes.Select(child => child.Position).Distinct().Count() == 2 ||
                itemNodes.GroupBy(child => child.Position).Any(group => group.Count() > 1 &&
                    group.Select(child => child.GetNode<TextureRect>("GroundItemIcon").Position).Distinct().Count() > 1),
                "同落点图标没有错开");
            foreach (var itemNode in itemNodes)
                Check(GameServices.Session.Map!.Objects.Values.Any(item =>
                    IsometricProjection.ToScreen(new Vector2(item.X, item.Y)).IsEqualApprox(itemNode.Position)),
                    "物品节点偏离权威实体坐标");
            // client-ui：GM 开关默认关闭，开启后每个标记在实体原点，改色即时同步。
            PressTitle(main, "GM"); await Frames();
            var gmPanel = (Control)main.FindChild("GM", true, false);
            var anchorToggle = (CheckButton)gmPanel.FindChild("ShowEntityAnchors", true, false);
            var anchorColor = (ColorPickerButton)gmPanel.FindChild("EntityAnchorColor", true, false);
            Check(!anchorToggle.ButtonPressed && anchorColor.Color == Colors.Yellow, "GM 锚点默认值错误");
            anchorToggle.ButtonPressed = true; await Frames();
            var actorAnchor = actorView.GetNode<EntityAnchorView>("EntityAnchor");
            Check(actorAnchor.Visible && itemNodes.All(node => node.GetNode<EntityAnchorView>("EntityAnchor").Visible),
                "GM 未显示角色或地面物品锚点");
            Check(itemNodes.All(node => node.GetNode<EntityAnchorView>("EntityAnchor").Position == Vector2.Zero),
                "地面物品锚点未处于真实实体坐标");
            anchorColor.Color = Colors.Cyan;
            anchorColor.EmitSignal(ColorPickerButton.SignalName.ColorChanged, Colors.Cyan);
            await Frames();
            Check(actorAnchor.MarkerColor == Colors.Cyan &&
                itemNodes.All(node => node.GetNode<EntityAnchorView>("EntityAnchor").MarkerColor == Colors.Cyan),
                "GM 锚点颜色未即时更新");
            anchorToggle.ButtonPressed = false; await Frames();
            Check(!actorAnchor.Visible && itemNodes.All(node => !node.GetNode<EntityAnchorView>("EntityAnchor").Visible),
                "关闭 GM 锚点后仍显示标记");
            Check(File.ReadAllText(Path.Combine(directory, ".env")).Contains("RPG_SHOW_ENTITY_ANCHORS=false"),
                "关闭锚点未保存 .env");
            using (var locked = new FileStream(Path.Combine(directory, ".env"), FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                anchorToggle.ButtonPressed = true; await Frames();
                Check(!anchorToggle.ButtonPressed && !GameServices.Env.ShowEntityAnchors && !actorAnchor.Visible &&
                    ((Label)main.FindChild("NetworkError", true, false)).Text.Contains("无法保存 .env 锚点设置"),
                    "锚点保存失败未恢复控件或显示原因");
            }
            PressTitle(main, "GM"); await Frames();

            // 数量窗打开时关闭背包，不允许继续确认。
            await ChooseGroundDrop(grid.GetChild<Control>(0));
            bag.Hide();
            await Frames();
            Check(!dialog.Visible, "关闭背包未关闭数量确认");
            Check(GameServices.Session.Bag.Items.Single().Count == 4, "关闭数量窗修改背包");
            GameServices.Net.Tick(5);
            var saved = repo.Load(1001).Bag;
            anchorToggle.ButtonPressed = true; await Frames();
            Check(File.ReadAllText(Path.Combine(directory, ".env")).Contains("RPG_SHOW_ENTITY_ANCHORS=true"),
                "开启锚点未保存 .env");
            main.QueueFree(); await Frames(); main = null;
            main = await StartMain(directory, config);
            Check(GameServices.Session.Bag.Revision == saved.Revision && GameServices.Session.Bag.Items.Select(item => (item.InstanceId, item.Count)).SequenceEqual(saved.Items.Select(item => (item.InstanceId, item.Count))), "重启后背包未恢复");
            Check(GameServices.Session.Map!.Objects.Count == 2, "重启后地图未恢复");
            view = main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Single();
            overlay = main.FindChildren("*", "Node", true, false).OfType<GroundItemOverlayControl>().Single();
            Check(view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "重启后地面表现未恢复");
            var restoredToggle = (CheckButton)main.FindChild("ShowEntityAnchors", true, false);
            var restoredActor = main.FindChildren("ActorView", "Node", true, false).OfType<ActorView>().Single();
            Check(GameServices.Env.ShowEntityAnchors && restoredToggle.ButtonPressed &&
                restoredActor.GetNode<EntityAnchorView>("EntityAnchor").Visible &&
                view.GetChildren().OfType<Node2D>().All(node => node.GetNode<EntityAnchorView>("EntityAnchor").Visible),
                "重启未恢复锚点开关或实体标记");
            using var moveOut = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
            GameServices.World.HandleInput(moveOut, new Vector2(23000, 23000));
            GameServices.Net.Tick(100);
            await Frames();
            Check(GameServices.Session.Map.Objects.Count == 0 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 0, "离开 AOI 未移除地面表现");
            using var moveBack = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
            GameServices.World.HandleInput(moveBack, new Vector2(ground.X, ground.Y));
            GameServices.Net.Tick(100);
            await Frames();
            Check(view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "再次进入 AOI 未恢复表现");
            // 对应部分/全部销毁与模式冻结，真实点击确认后地面数量始终不变。
            bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            bag.Show(); await Frames();
            grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            dialog = (ConfirmationDialog)bag.FindChild("DropItemDialog", true, false);
            count = (SpinBox)dialog.FindChild("DropItemCount", true, false);
            var targetBeforeDestroy = GameServices.World.RoleTarget;
            await ChooseGroundDrop(grid.GetChild<Control>(0), true);
            Check(dialog.Visible && count.Value == 4, "整叠销毁未默认当前数量");
            count.GetLineEdit().Text = "2";
            dialog.GetOkButton().EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(GameServices.Session.Bag.Items.Single().Count == 2 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "部分销毁数量或地面状态错误");
            await ChooseGroundDrop(grid.GetChild<Control>(0), true);
            dialog.GetOkButton().EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(GameServices.Session.Bag.Items.Count == 0 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "全部销毁未移除源或生成了地面对象");
            GameServices.Session.SendGm("item 1012 1"); await Frames();
            await ChooseGroundDrop(grid.GetChild<Control>(0), true);
            Check(dialog.Visible && count.Value == 1, "单件销毁跳过了确认");
            dialog.GetOkButton().EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(GameServices.Session.Bag.Items.Count == 0 && view.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 2, "单件销毁错误");
            Check(GameServices.World.RoleTarget == targetBeforeDestroy, "销毁点击触发了走路");
            main.QueueFree(); await Frames(); main = null;
            main = await StartMain(directory, config);
            Check(GameServices.Session.Bag.Items.Count == 0 && GameServices.Session.Map!.Objects.Count == 2, "关闭重启复活销毁物品或改变地面");
            // ground-item-drop：信息框悬停联动轮廓，点击走现有拾取且不穿透到地图。
            view = main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Single();
            overlay = main.FindChildren("*", "Node", true, false).OfType<GroundItemOverlayControl>().Single();
            bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            bag.Hide();
            SendKey(Key.Z); await Frames();
            var pickupInfo = overlay.FindChildren("*", "PanelContainer", true, false).OfType<GroundItemInfoControl>()
                .First(info => info.Visible);
            var infoPoint = pickupInfo.GetGlobalRect().GetCenter();
            MouseMotionAt(infoPoint); await Frames();
            Check(pickupInfo.IsHighlighted && view.HoveredObjectId == pickupInfo.ObjectId &&
                view.GetNode<Node2D>("Item_" + pickupInfo.ObjectId.ToString("N"))
                    .GetNode<TextureRect>("GroundItemOutline").Visible,
                "信息框悬停未联动高亮");
            var beforeInfoPickup = GameServices.Session.Map.Objects.Count;
            var infoClicked = false;
            pickupInfo.PickupRequested += _ => infoClicked = true;
            MouseButtonAt(infoPoint, true); MouseButtonAt(infoPoint, false);
            GameServices.Net.Tick(3); GameServices.World.Tick(3); await Frames();
            Check(infoClicked && (GameServices.Session.Map.Objects.Count < beforeInfoPickup || GameServices.World.RoleTarget is not null),
                "点击信息框未发起拾取");
            Check(!main.FindChildren("*", "Node", true, false).OfType<GroundClickView>().Any(),
                "点击信息框触发了普通地图反馈");
            PressTitle(main, "系统"); PressTitle(main, "返回角色选择"); await Frames();
            Check(!main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Any(), "离开世界未释放表现");
            GD.Print("Ground item drop smoke passed: quantity, Ctrl destroy confirmation/cancel/partial/full/single/restart, partial/full, stale confirmation, UI input isolation, icons, AOI, restart.");
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
    private async Task ChooseGroundDrop(Control slot, bool ctrl = false)
    {
        var point = slot.GetGlobalRect().GetCenter();
        MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
        point = new Vector2(20, 220);
        MouseButtonAt(point, true, ctrl); MouseButtonAt(point, false); await Frames();
    }

    /// <summary>进入真实角色和背包，正式物品配置仍由隔离文件提供。</summary>
    private async Task<DoubleMainController> StartMain(string directory, string config)
    {
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"), ConfigDirectory = config,
            EnvFilePath = Path.Combine(directory, ".env") };
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
    private void MouseButtonAt(Vector2 point, bool pressed, bool ctrl = false)
    {
        using var button = new InputEventMouseButton { Position = point, GlobalPosition = point,
            ButtonIndex = MouseButton.Left, Pressed = pressed, CtrlPressed = ctrl, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
        GetViewport().PushInput(button, true);
    }

    /// <summary>移动真实指针以触发信息框悬停状态。</summary>
    private void MouseMotionAt(Vector2 point)
    {
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
    }

    /// <summary>通过视口提交快捷键按下和释放。</summary>
    private void SendKey(Key key)
    {
        using var press = new InputEventKey { Keycode = key, Pressed = true };
        using var release = new InputEventKey { Keycode = key, Pressed = false };
        GetViewport().PushInput(press, true);
        GetViewport().PushInput(release, true);
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
