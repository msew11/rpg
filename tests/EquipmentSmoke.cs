using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>role-equipment：通过真实角色窗口和鼠标输入验收七件装备、替换、卸下与旋转。</summary>
public partial class EquipmentSmoke : Node
{
    /// <summary>隔离存档并通过正式节点走完整穿脱流程。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-equipment-ui-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            var repo = new RepoServerRole(Path.Combine(directory, "server"));
            repo.SaveBag(repo.Load(1001) with { Bag = new DataServerBag { NextInstanceId = 9,
                Items = Enumerable.Range(1012, 7).Select((id, index) => new DataServerItem(index + 1, id, index, 1))
                    .Append(new DataServerItem(8, 1017, 7, 1)).ToArray() } });
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
            AddChild(main);
            await Frames();
            PressTitle(main, "登录");
            await Frames();
            ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            PressTitle(main, "角色");
            PressTitle(main, "背包");
            await Frames();
            var hud = main.FindChildren("*", "Control", true, false).OfType<GameHud>().Single();
            var character = hud.GetNode<ViewportPanelControl>("角色");
            var bag = hud.GetNode<BagControl>("背包");
            // 通过真实标题拖动分开两个窗口，避免测试直接覆写布局状态。
            await MoveWindow(character, new Vector2(20, 20));
            await MoveWindow(bag, new Vector2(650, 80));
            Check(character.FindChildren("Equipment*", "PanelContainer", true, false).OfType<EquipmentSlotControl>().Count() == 11,
                "角色窗口没有十一种独立槽位");
            var preview = (RolePreviewControl)character.FindChild("EquipmentPreview", true, false);
            var previewActor = (ActorView)preview.FindChild("PreviewActor", true, false);
            Check(!previewActor.GetNode<Sprite2D>("Facing/RightHand/Weapon").Visible && GameServices.World.ActorState!.MainHandId == 0,
                "初始空手仍显示长剑");
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            var target = GameServices.World.RoleTarget;
            foreach (var id in Enumerable.Range(1012, 7))
            {
                var item = GameServices.Session.Bag.Items.Single(item => item.ItemId == id && item.InstanceId != 8);
                var slot = id is 1014 or 1018 ? EquipmentSlot.OffHand : EquipmentSlot.MainHand;
                var control = (Control)character.FindChild($"Equipment{slot}", true, false);
                await Transfer(grid.GetChild<Control>(item.SlotIndex), control);
                Check(GameServices.Session.Bag.Equipment.Any(item => item.ItemId == id && item.Slot == slot), $"装备失败：{id}");
                var state = GameServices.World.ActorState!;
                Check((slot == EquipmentSlot.MainHand ? state.MainHandId : state.OffHandId) == id, "权威装备未同步世界");
                var sprite = previewActor.GetNode<Sprite2D>(slot == EquipmentSlot.MainHand ? "Facing/RightHand/Weapon" : "Facing/LeftHand/Weapon");
                Check(sprite.Visible && sprite.Texture is not null, "预览未显示实际装备");
            }
            var off = (Control)character.FindChild("EquipmentOffHand", true, false);
            var extra = GameServices.Session.Bag.Items.Single(item => item.InstanceId == 8);
            await Transfer(grid.GetChild<Control>(extra.SlotIndex), off);
            Check(GameServices.Session.Bag.Equipment.Count(item => item.ItemId == 1017) == 2, "双拳刃未同时显示");
            var direction = preview.Direction;
            await Click((Control)preview.FindChild("RotateClockwise", true, false));
            Check(preview.Direction == (direction + 1) % 8, "预览旋转没有生效");
            var mainSlot = (Control)character.FindChild("EquipmentMainHand", true, false);
            var empty = Enumerable.Range(0, 40).First(index => GameServices.Session.Bag.Items.All(item => item.SlotIndex != index));
            await Transfer(mainSlot, grid.GetChild<Control>(empty));
            Check(GameServices.World.ActorState!.MainHandId == 0 && !previewActor.GetNode<Sprite2D>("Facing/RightHand/Weapon").Visible,
                "卸下主手没有空手");
            await Click(off, MouseButton.Right);
            Check(GameServices.Session.Bag.Equipment.Count == 0 && GameServices.World.ActorState!.OffHandId == 0,
                "右键卸下副手失败");
            // 飞刀错误放主手仍由服务器拒绝，界面展示原因且保留物品。
            var knife = GameServices.Session.Bag.Items.Single(item => item.ItemId == 1018);
            var revision = GameServices.Session.Bag.Revision;
            await Transfer(grid.GetChild<Control>(knife.SlotIndex), mainSlot);
            Check(GameServices.Session.Bag.Revision == revision && character.FindChild("EquipmentStatus", true, false) is Label { Visible: true },
                "错误槽位未反馈或修改了物品");
            Check(GameServices.World.RoleTarget == target, "装备输入穿透到世界移动");
            // 保留一剑一盾用于可选的真实渲染检查。
            foreach (var id in new[] { 1012, 1014 })
            {
                var item = GameServices.Session.Bag.Items.Single(item => item.ItemId == id);
                await Transfer(grid.GetChild<Control>(item.SlotIndex), (Control)character.FindChild(id == 1012 ? "EquipmentMainHand" : "EquipmentOffHand", true, false));
            }
            if (OS.GetCmdlineUserArgs().Contains("--capture-equipment"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/equipment-window.png"));
            }
            GameServices.Net.FlushServerWrites();
            Check(repo.Load(1001).Bag.Equipment.Count == 2, "装备未持久化");
            GetWindow().Size = new Vector2I(480, 270);
            GetWindow().ContentScaleSize = new Vector2I(480, 270);
            await Frames();
            Check(character.Position.X >= 0 && character.Position.Y >= 0 && character.Size.X <= 480 && character.Size.Y <= 270,
                "小窗口装备面板越界");
            var scroll = (ScrollContainer)character.FindChild("PanelContentScroll", true, false);
            scroll.EnsureControlVisible((Control)character.FindChild("EquipmentFeet", true, false));
            await Frames();
            Check(scroll.ScrollVertical > 0, "小窗口不能滚动至鞋子槽");
            GD.Print("Equipment smoke passed: 7 items, swap, dual katars, empty hands, rotation, invalid slot, persistence, real input and small viewport.");
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

    /// <summary>通过真实标题输入移动窗口，保持普通窗口拖动契约。</summary>
    private async Task MoveWindow(Control panel, Vector2 position)
    {
        var header = (Control)panel.FindChild("PanelHeader", true, false);
        var point = header.GetGlobalRect().Position + new Vector2(18, 15);
        Mouse(point, MouseButton.Left, true);
        using var motion = new InputEventMouseMotion { Position = point + position - panel.Position, GlobalPosition = point + position - panel.Position };
        GetViewport().PushInput(motion, true);
        Mouse(motion.Position, MouseButton.Left, false);
        await Frames();
    }

    /// <summary>两次真实点击完成物品转移，确认前不得改变权威缓存。</summary>
    private async Task Transfer(Control source, Control target)
    {
        var before = GameServices.Session.Bag;
        await Click(source);
        Check(ReferenceEquals(before, GameServices.Session.Bag), "拿起装备提前修改了快照");
        await Click(target);
    }

    /// <summary>实际注入移动、按下及松开以覆盖 GUI 路由。</summary>
    private async Task Click(Control control, MouseButton button = MouseButton.Left)
    {
        var point = control.GetGlobalRect().GetCenter();
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
        Mouse(point, button, true);
        Mouse(point, button, false);
        await Frames();
    }

    /// <summary>构造独立鼠标事件，不直接调用业务回调。</summary>
    private void Mouse(Vector2 position, MouseButton button, bool pressed)
    {
        using var input = new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button,
            Pressed = pressed, ButtonMask = pressed ? button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right : 0 };
        GetViewport().PushInput(input, true);
    }

    /// <summary>仅页面导航使用现有按钮入口，物品操作全部通过鼠标。</summary>
    private static void PressTitle(Node root, string title) => root.FindChildren("*", "Button", true, false).OfType<Button>()
        .First(button => button.Text == title && button.IsVisibleInTree()).EmitSignal(Button.SignalName.Pressed);

    /// <summary>推进布局、网络与固定帧。</summary>
    private async Task Frames() { for (var index = 0; index < 8; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    /// <summary>明确失败原因并交由场景退出码报告。</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
