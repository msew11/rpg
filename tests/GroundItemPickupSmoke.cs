using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Rpg.Items;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 ground-item-pickup 的真实命中、轮廓、Fetch、走近拾取与 UI 输入隔离。</summary>
public partial class GroundItemPickupSmoke : Node
{
    /// <summary>真实界面覆盖拾取闭环，不调用拾取节点的内部回调。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-pickup-ui-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var config = Path.Combine(directory, "config");
            var server = Path.Combine(directory, "server");
            Directory.CreateDirectory(config);
            foreach (var name in new[] { "basic", "map", "item" })
                File.Copy(ProjectSettings.GlobalizePath($"res://config/{name}.json"), Path.Combine(config, name + ".json"));
            var definitions = JsonNode.Parse(File.ReadAllText(Path.Combine(config, "item.json")))!.AsArray();
            definitions.Add(new JsonObject { ["id"] = 2000, ["name"] = "拾取验收", ["category"] = 0, ["subCategory"] = 0, ["maxStack"] = 10 });
            File.WriteAllText(Path.Combine(config, "item.json"), definitions.ToJsonString());
            RoleTestData.Seed(server);
            var repo = new RepoServerRole(server);
            repo.SaveBag(repo.Load(1001) with { Bag = new() { NextInstanceId = 3,
                Items = [new(1, 1012, 0, 1), new(2, 2000, 1, 5)] } });
            main = await StartMain(directory, config);
            var bag = main.FindChildren("*", "Control", true, false).OfType<BagControl>().Single();
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            var quantity = (ConfirmationDialog)bag.FindChild("DropItemDialog", true, false);
            await DropAtGround(grid.GetChild<Control>(0));
            Check(!quantity.Visible && GameServices.Session.Map!.Objects.Count == 1, "单件没有直接丢弃");
            bag.Hide(); await Frames();
            var groundView = main.FindChildren("*", "Node", true, false).OfType<GroundItemsView>().Single();
            var cursor = main.FindChildren("*", "Node", true, false).OfType<PointerCursor>().Single();
            var icon = groundView.FindChildren("GroundItemIcon", "TextureRect", true, false).OfType<TextureRect>().Single();
            var groundAsset = GameServices.Config.Item.Items[1012].GroundAssetId;
            Check(groundAsset == "item/item/item1001", "地面素材未按来源小图编号绑定");
            var expectedGround = new DataItemOutline(new ItemIconResolver().Resolve(groundAsset));
            var inventoryIcon = new DataItemOutline(new ItemIconResolver().Resolve(GameServices.Config.Item.Items[1012].IconAssetId));
            Check(icon.Texture.GetImage().GetData().SequenceEqual(expectedGround.Icon.GetImage().GetData()), "地面表现未使用独立地面纹理");
            Check(!icon.Texture.GetImage().GetData().SequenceEqual(inventoryIcon.Icon.GetImage().GetData()), "地面仍然复用了背包大图");
            // 扩展框内无图像素也必须能通过实际点击拾取。
            var point = icon.GetGlobalTransformWithCanvas() * new Vector2(-6, 16);
            Motion(point); await Frames();
            Check(groundView.HoveredObjectId is not null && cursor.IsItemHovering, "悬停未命中地面物品或未切 Fetch");
            var outline = (TextureRect)groundView.FindChild("GroundItemOutline", true, false);
            var namePopup = (ItemNamePopup)groundView.FindChild("ItemNamePopup", true, false);
            Check(namePopup.Visible && namePopup.Text == GameServices.Config.Item.Items[1012].Name, "地面未复用即时 name 浮层");
            Check(bag.FindChild("ItemNamePopup", true, false) is ItemNamePopup, "背包未使用共用名称控件");
            Check(outline.Visible && cursor.CurrentHotspot.IsEqualApprox(new Vector2(7.5f, 7.5f)), "轮廓或 Fetch 热点错误");
            var openHand = cursor.CurrentTexture;
            MouseButtonAt(point, true);
            Check(cursor.CurrentTexture != openHand, "按下未显示 Fetch 握手");
            MouseButtonAt(point, false);
            GameServices.Net.Tick(2); GameServices.World.Tick(2); await Frames();
            Check(GameServices.Session.Map!.Objects.Count == 0 && GameServices.Session.Bag.Items.Any(item => item.ItemId == 1012), "近距离点击没有拾回背包");
            Check(!cursor.IsItemHovering && groundView.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 0, "拾取后仍残留 Fetch 或地面表现");
            Check(!main.FindChildren("*", "Node", true, false).OfType<GroundClickView>().Any(), "点击物品同时触发普通点地反馈");

            // 再丢出有图物品，验证透明区域、扩展范围、悬停名称、遮挡和失焦。
            PressTitle(main, "背包"); await Frames();
            var sword = GameServices.Session.Bag.Items.Single(item => item.ItemId == 1012);
            await DropAtGround(grid.GetChild<Control>(sword.SlotIndex));
            bag.Hide(); await Frames();
            icon = groundView.FindChildren("GroundItemIcon", "TextureRect", true, false).OfType<TextureRect>().Single();
            point = PixelPoint(icon, true);
            Motion(PixelPoint(icon, false)); await Frames();
            Check(groundView.HoveredObjectId is not null && cursor.IsItemHovering, "透明像素未命中完整显示框");
            Motion(icon.GetGlobalTransformWithCanvas() * new Vector2(-7, 16)); await Frames();
            Check(groundView.HoveredObjectId is not null && cursor.IsItemHovering, "扩展区域未触发高亮");
            Motion(icon.GetGlobalTransformWithCanvas() * new Vector2(-9, -9)); await Frames();
            Check(groundView.HoveredObjectId is null && !cursor.IsItemHovering, "超出扩展区域仍触发高亮");
            Check(!namePopup.Visible, "移出物品后名称仍显示");
            Motion(point); await Frames();
            Check(namePopup.Visible && namePopup.Text == GameServices.Config.Item.Items[1012].Name, "悬停名称不正确");
            Check((namePopup.Position + namePopup.Size).X <= GetViewport().GetVisibleRect().Size.X, "名称越出视口");
            var overlayLayer = new CanvasLayer { Layer = 30 };
            var blocker = new Panel { Position = point - new Vector2(25, 25), Size = new Vector2(50, 50), MouseFilter = Control.MouseFilterEnum.Stop };
            overlayLayer.AddChild(blocker); main.AddChild(overlayLayer);
            Motion(point + Vector2.One); Motion(point); await Frames();
            Check(!namePopup.Visible && !cursor.IsItemHovering && groundView.HoveredObjectId is null, "覆盖 UI 后仍有拾取高亮");
            var before = GameServices.Session.Bag.Revision;
            MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
            Check(GameServices.Session.Bag.Revision == before && GameServices.Session.Map!.Objects.Count == 1, "UI 点击穿透拾取");
            overlayLayer.QueueFree(); await Frames(); Motion(point); await Frames();
            var modal = new ConfirmationDialog { Exclusive = true }; main.AddChild(modal); modal.PopupCentered(); await Frames();
            Motion(point); await Frames();
            Check(!namePopup.Visible && !cursor.IsItemHovering, "模态窗口未阻断 Fetch");
            modal.Hide(); modal.QueueFree(); await Frames(); Motion(point); await Frames();
            groundView.Notification((int)Node.NotificationApplicationFocusOut);
            cursor.Notification((int)Node.NotificationApplicationFocusOut);
            await Frames();
            Check(!namePopup.Visible && !cursor.IsItemHovering && groundView.HoveredObjectId is null, "失焦残留悬停");
            groundView.Notification((int)Node.NotificationApplicationFocusIn);
            cursor.Notification((int)Node.NotificationApplicationFocusIn);
            Motion(point); await Frames();

            // 移远后再点击，须先走近，不能点击瞬间远程取物。
            var item = GameServices.Session.Map!.Objects.Values.Single();
            GameServices.World.HandleInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, new Vector2(item.X + 300, item.Y));
            GameServices.Net.Tick(2); GameServices.World.Tick(2); await Frames();
            icon = groundView.FindChildren("GroundItemIcon", "TextureRect", true, false).OfType<TextureRect>().Single();
            point = PixelPoint(icon, true); Motion(point); await Frames();
            MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
            Check(GameServices.Session.Map.Objects.Count == 1 && GameServices.World.RoleTarget is not null, "远处没有走近就拾取");
            GameServices.Net.Tick(0.2); await Frames();
            Check(GameServices.Session.Map.Objects.Count == 1, "超出服务端拾取距离仍成功");
            GameServices.Net.Tick(2); GameServices.World.Tick(2); await Frames();
            Check(GameServices.Session.Map.Objects.Count == 0 && GameServices.Session.Bag.Items.Any(item => item.ItemId == 1012), "走近后没有自动拾取");

            // 单件修改不影响多件数量窗口，失败反馈留在 HUD。
            PressTitle(main, "背包"); await Frames();
            var stack = GameServices.Session.Bag.Items.Single(item => item.ItemId == 2000);
            await DropAtGround(grid.GetChild<Control>(stack.SlotIndex));
            Check(quantity.Visible, "多件丢弃未打开数量窗");
            ((SpinBox)quantity.FindChild("DropItemCount", true, false)).GetLineEdit().Text = "2";
            quantity.GetOkButton().EmitSignal(Button.SignalName.Pressed); await Frames();
            bag.Hide();
            GameServices.Session.SendGm("item 2000 387"); await Frames();
            icon = groundView.FindChildren("GroundItemIcon", "TextureRect", true, false).OfType<TextureRect>().Single();
            point = PixelPoint(icon, true); Motion(point); await Frames();
            MouseButtonAt(point, true); MouseButtonAt(point, false);
            GameServices.Net.Tick(2); GameServices.World.Tick(2); await Frames();
            Check(GameServices.Session.Map.Objects.Count == 1 && ((Label)main.FindChild("NetworkError", true, false)).Text.Contains("背包空间不足"), "背包满未保留地面物品和反馈");
            GameServices.Net.Tick(5);
            var savedTotal = repo.Load(1001).Bag.Items.Sum(item => item.Count);
            main.QueueFree(); await Frames(); main = null;
            main = await StartMain(directory, config);
            Check(GameServices.Session.Bag.Items.Sum(item => item.Count) == savedTotal && GameServices.Session.Map!.Objects.Count == 1, "重启未恢复拾取和失败后的完整状态");
            GD.Print("Ground item pickup smoke passed: alpha outline, Fetch, UI/modal/focus, near/far pickup, capacity, single direct drop, quantity, restart.");
            main.QueueFree(); await Frames(); main = null; GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); main?.QueueFree(); await Frames(); main = null; GetTree().Quit(1);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>返回图标中已知透明或非透明像素的视口坐标，包含摄像机变换。</summary>
    private static Vector2 PixelPoint(TextureRect icon, bool opaque)
    {
        var image = icon.Texture.GetImage();
        for (var y = 0; y < image.GetHeight(); y++)
        for (var x = 0; x < image.GetWidth(); x++)
            if (image.GetPixel(x, y).A > 0.1f == opaque)
                return icon.GetGlobalTransformWithCanvas() * new Vector2(x + 0.5f, y + 0.5f);
        throw new InvalidOperationException("图标缺少验收像素");
    }

    /// <summary>真实点击拿起后点地图，不直接调用丢弃处理函数。</summary>
    private async Task DropAtGround(Control slot)
    {
        var point = slot.GetGlobalRect().GetCenter();
        Motion(point); MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
        point = new Vector2(20, 220);
        Motion(point); MouseButtonAt(point, true); MouseButtonAt(point, false); await Frames();
    }

    /// <summary>注入真实鼠标移动以触发 GUI 遮挡和扩展范围命中。</summary>
    private void Motion(Vector2 point)
    {
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
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
        // 指针在地面表现之前更新，需要完成两次处理帧后再读取反馈。
        for (var index = 0; index < 3; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>将验收失败转换为场景非零退出。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
