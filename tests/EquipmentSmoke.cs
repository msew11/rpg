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

/// <summary>openspec/specs/role-equipment、role-management 与 client-ui：验收统一拿起、即时信息、侧身旋转及真实穿脱。</summary>
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
            await MoveWindow(character, new Vector2(280, 80));
            await MoveWindow(bag, new Vector2(650, 80));
            Check(character.FindChildren("Equipment*", "PanelContainer", true, false).OfType<EquipmentSlotControl>().Count() == 11,
                "角色窗口没有十一种独立槽位");
            var preview = (RolePreviewControl)character.FindChild("EquipmentPreview", true, false);
            var previewActor = (ActorView)preview.FindChild("PreviewActor", true, false);
            Check(preview.Direction == 0 && previewActor.GetNode<AnimatedSprite2D>("Facing/Body").Animation == "dir1" &&
                previewActor.GetNode<Node2D>("Facing").Scale.X == -1, "装备展示默认未向右侧身");
            Check(previewActor.Scale == Vector2.One &&
                preview.FindChild("PreviewPortrait", true, false) is AvatarControl { Visible: false },
                "装备预览未使用地图原始比例或仍显示头像");
            var grid = (GridContainer)bag.FindChild("BagGrid", true, false);
            var bagSlotSize = grid.GetChild<Control>(0).Size;
            var feet = (Control)character.FindChild("EquipmentFeet", true, false);
            var mainHand = (Control)character.FindChild("EquipmentMainHand", true, false);
            var offHand = (Control)character.FindChild("EquipmentOffHand", true, false);
            Check(bagSlotSize == new Vector2(48, 48), "背包格子基准尺寸改变");
            foreach (var hand in new[] { mainHand, offHand })
                Check(Mathf.IsEqualApprox(hand.GlobalPosition.Y, feet.GlobalPosition.Y) &&
                    Mathf.IsEqualApprox(hand.GetGlobalRect().End.Y, feet.GetGlobalRect().End.Y) &&
                    hand.GlobalPosition.Y >= preview.GetGlobalRect().End.Y,
                    "主副手未在人物下方与鞋子同行");
            var leftInset = feet.GlobalPosition.X - character.GlobalPosition.X;
            var rightSlot = (Control)character.FindChild("EquipmentFace", true, false);
            var rightInset = character.GetGlobalRect().End.X - rightSlot.GetGlobalRect().End.X;
            Check(rightInset <= leftInset + 5 && character.Size.X < 340, "角色窗口右侧仍有多余固定留白");
            foreach (var slotControl in character.FindChildren("Equipment*", "PanelContainer", true, false).OfType<EquipmentSlotControl>())
            {
                var name = slotControl.GetNode<Label>("SlotName");
                var color = name.GetThemeColor("font_color");
                Check(name.Visible && slotControl.GetGlobalRect().Encloses(name.GetGlobalRect()) &&
                    Mathf.IsEqualApprox(color.R, color.G) && Mathf.IsEqualApprox(color.G, color.B) && color.R < 0.7f,
                    "空槽名称未在槽内以灰色展示");
                Check(slotControl.Size == bagSlotSize && slotControl.FindChild("ItemCount", true, false) is null,
                    "装备槽与背包不等大或带有数量");
            }
            foreach (var buttonName in new[] { "RotateCounterclockwise", "RotateClockwise" })
            {
                var button = (Button)preview.FindChild(buttonName, true, false);
                Check(button.Text.Length == 0 && button.Icon is not null && button.Size == new Vector2(28, 28) &&
                    button.Icon.GetSize() == new Vector2(18, 18) && button.IconAlignment == HorizontalAlignment.Center &&
                    button.GetGlobalRect().End.Y < mainHand.GlobalPosition.Y,
                    "旋转按钮未缩小居中或未上移到手部槽上方");
            }
            Check(!character.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("右键卸下")),
                "仍显示穿脱操作说明");
            Check(!previewActor.GetNode<Sprite2D>("Facing/RightHand/Weapon").Visible && GameServices.World.ActorState!.MainHandId == 0,
                "初始空手仍显示长剑");
            var target = GameServices.World.RoleTarget;
            foreach (var id in Enumerable.Range(1012, 7))
            {
                var item = GameServices.Session.Bag.Items.Single(item => item.ItemId == id && item.InstanceId != 8);
                var slot = id is 1014 or 1018 ? EquipmentSlot.OffHand : EquipmentSlot.MainHand;
                var control = (Control)character.FindChild($"Equipment{slot}", true, false);
                var bagIcon = (TextureRect)grid.GetChild<Control>(item.SlotIndex).FindChild("ItemIcon", true, false);
                var iconSize = bagIcon.Size;
                var sourceSize = bagIcon.Texture.GetSize();
                var iconStretch = bagIcon.StretchMode;
                var iconFilter = bagIcon.TextureFilter;
                await Click(grid.GetChild<Control>(item.SlotIndex));
                var bagPicked = PickedAppearance(bag);
                await Click(control);
                var equippedIcon = (TextureRect)control.FindChild("EquipmentIcon", true, false);
                Check(equippedIcon.Size == iconSize && equippedIcon.Texture.GetSize() == sourceSize &&
                    equippedIcon.StretchMode == iconStretch && equippedIcon.TextureFilter == iconFilter,
                    "装备图标与背包的实际显示尺寸或采样方式不同");
                Check(GameServices.Session.Bag.Equipment.Any(item => item.ItemId == id && item.Slot == slot), $"装备失败：{id}");
                var state = GameServices.World.ActorState!;
                Check((slot == EquipmentSlot.MainHand ? state.MainHandId : state.OffHandId) == id, "权威装备未同步世界");
                var sprite = previewActor.GetNode<Sprite2D>(slot == EquipmentSlot.MainHand ? "Facing/RightHand/Weapon" : "Facing/LeftHand/Weapon");
                Check(sprite.Visible && sprite.Texture is not null, "预览未显示实际装备");
                // 隐藏背包仍立即显示装备信息，复用同一个浮窗且没有引擎延迟提示。
                bag.Hide();
                await Frames();
                Hover(control);
                var info = (ItemNamePopup)bag.FindChild("ItemNamePopup", true, false);
                Check(info.IsVisibleInTree() && info.Text == GameServices.Config.Item.Items[id].Name && control.TooltipText == "",
                    "背包隐藏时装备没有即时共用名称框");
                Hover(feet);
                Check(!info.Visible, "空槽仍显示上一装备信息");
                Hover(control);
                character.Hide();
                await Frames();
                Check(!info.Visible, "关闭角色窗口残留道具信息");
                character.Show();
                await Frames();
                await Click(control);
                Check(!info.Visible && PickedAppearance(bag) == bagPicked, "两处拿起的图像尺寸、数量、采样或透明度不一致");
                var picked = (Control)bag.FindChild("PickedItemPreview", true, false);
                Check(picked.GetGlobalRect().GetCenter().DistanceTo(control.GetGlobalRect().GetCenter()) < 0.1f,
                    "装备拿起图像未以鼠标为中心");
                await Click(control);
            }
            var off = (Control)character.FindChild("EquipmentOffHand", true, false);
            var extra = GameServices.Session.Bag.Items.Single(item => item.InstanceId == 8);
            await Transfer(grid.GetChild<Control>(extra.SlotIndex), off);
            Check(GameServices.Session.Bag.Equipment.Count(item => item.ItemId == 1017) == 2, "双拳刃未同时显示");
            var direction = preview.Direction;
            await Click((Control)preview.FindChild("RotateClockwise", true, false));
            Check(preview.Direction == (direction + 7) % 8, "右按钮没有使人物顺时针自转");
            await Click((Control)preview.FindChild("RotateCounterclockwise", true, false));
            Check(preview.Direction == direction, "左按钮没有逆时针抵消旋转");
            var mainSlot = (Control)character.FindChild("EquipmentMainHand", true, false);
            var empty = Enumerable.Range(0, 40).First(index => GameServices.Session.Bag.Items.All(item => item.SlotIndex != index));
            await Transfer(mainSlot, grid.GetChild<Control>(empty));
            Check(GameServices.World.ActorState!.MainHandId == 0 && !previewActor.GetNode<Sprite2D>("Facing/RightHand/Weapon").Visible,
                "卸下主手没有空手");
            await Click(off, MouseButton.Right);
            Check(GameServices.Session.Bag.Equipment.Count == 1 && bag.FindChild("PickedItemPreview", true, false) is null,
                "右键仍会卸下或拿起装备");
            await Click(off, ctrl: true);
            Check(GameServices.Session.Bag.Equipment.Count == 0 && GameServices.World.ActorState!.OffHandId == 0,
                "Ctrl＋左键卸下副手失败");
            // 飞刀错误放主手仍由服务器拒绝，界面展示原因且保留物品。
            var knife = GameServices.Session.Bag.Items.Single(item => item.ItemId == 1018);
            var revision = GameServices.Session.Bag.Revision;
            await Transfer(grid.GetChild<Control>(knife.SlotIndex), mainSlot);
            Check(GameServices.Session.Bag.Revision == revision && character.FindChild("EquipmentStatus", true, false) is Label { Visible: true },
                "错误槽位未反馈或修改了物品");
            Check(GameServices.World.RoleTarget == target, "装备输入穿透到世界移动");
            // Ctrl 快捷穿戴覆盖主副手推荐、拳刃默认槽及替换回来源格。
            foreach (var id in new[] { 1013, 1017, 1012, 1014 })
            {
                var item = GameServices.Session.Bag.Items.Single(item => item.ItemId == id && item.InstanceId != 8);
                var slot = id == 1014 ? EquipmentSlot.OffHand : EquipmentSlot.MainHand;
                var oldItem = GameServices.Session.Bag.Equipment.FirstOrDefault(entry => entry.Slot == slot);
                var before = GameServices.Session.Bag;
                await Click(grid.GetChild<Control>(item.SlotIndex), ctrl: true, checkBeforeFrames: () =>
                    Check(ReferenceEquals(before, GameServices.Session.Bag), "Ctrl 穿戴在确认前改写快照"));
                Check(GameServices.Session.Bag.Equipment.Any(entry => entry.InstanceId == item.InstanceId && entry.Slot == slot),
                    "Ctrl 自动穿戴选槽错误");
                if (oldItem is not null)
                    Check(GameServices.Session.Bag.Items.Any(entry => entry.InstanceId == oldItem.InstanceId && entry.SlotIndex == item.SlotIndex),
                        "Ctrl 替换没有回来源格");
                Check(((Control)character.FindChild($"Equipment{slot}", true, false)).GetNode<Label>("SlotName").Visible == false &&
                    bag.FindChild("PickedItemPreview", true, false) is null, "快捷穿戴残留占位文字或拿起预览");
            }
            Check(GameServices.World.RoleTarget == target, "Ctrl 装备输入穿透到世界移动");
            if (OS.GetCmdlineUserArgs().Contains("--capture-equipment"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/equipment-window.png"));
            }
            GameServices.Net.FlushServerWrites();
            Check(repo.Load(1001).Bag.Equipment.Count == 2, "装备未持久化");
            var capacity = GameServices.Config.Basic.Value.BagColumns * GameServices.Config.Basic.Value.BagRows;
            GameServices.Session.SendGm($"item 1012 {capacity - GameServices.Session.Bag.Items.Count}");
            await Frames();
            Check(GameServices.Session.Bag.Items.Count == capacity, "满包卸下验收未填满背包");
            var fullBag = GameServices.Session.Bag;
            await Click(mainSlot, ctrl: true);
            Check(ReferenceEquals(fullBag, GameServices.Session.Bag) &&
                character.FindChild("EquipmentStatus", true, false) is Label { Visible: true },
                "满包 Ctrl 卸下没有保留装备并反馈错误");
            GetWindow().Size = new Vector2I(480, 270);
            GetWindow().ContentScaleSize = new Vector2I(480, 270);
            await Frames();
            Check(character.Position.X >= 0 && character.Position.Y >= 0 && character.Size.X <= 480 && character.Size.Y <= 270,
                "小窗口装备面板越界");
            var scroll = (ScrollContainer)character.FindChild("PanelContentScroll", true, false);
            scroll.EnsureControlVisible((Control)character.FindChild("EquipmentFeet", true, false));
            await Frames();
            Check(scroll.ScrollVertical > 0, "小窗口不能滚动至鞋子槽");
            foreach (var hand in new[] { mainHand, offHand })
                Check(scroll.GetGlobalRect().Encloses(hand.GetGlobalRect()) && hand.Size == bagSlotSize,
                    "小窗口手部槽无法与鞋子同时显示或尺寸改变");
            if (OS.GetCmdlineUserArgs().Contains("--capture-equipment"))
            {
                bag.Hide();
                hud.MoveChild(character, hud.GetChildCount() - 1);
                await Frames();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/equipment-window-small.png"));
            }
            GD.Print("Equipment smoke passed: compact preview, slot labels, icons, Ctrl equip/swap/unequip, full bag rejection, inert right click, 7 items, persistence and small viewport.");
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

    /// <summary>比较实际拿起图像的渲染区域、纹理、数量和透明度，而非只比较格子。</summary>
    private static (Vector2, Vector2, string, CanvasItem.TextureFilterEnum, Color, string) PickedAppearance(BagControl bag)
    {
        var picked = (Control)bag.FindChild("PickedItemPreview", true, false);
        var icon = (TextureRect)picked.FindChild("ItemIcon", true, false);
        using var pixels = icon.Texture.GetImage();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels.GetData()));
        return (picked.Size, icon.Size, hash, icon.TextureFilter, picked.Modulate,
            ((Label)picked.FindChild("ItemCount", true, false)).Text);
    }

    /// <summary>仅注入鼠标移动，验证悬停当帧出现信息。</summary>
    private void Hover(Control control)
    {
        var point = control.GetGlobalRect().GetCenter();
        using var input = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(input, true);
    }

    /// <summary>实际注入移动、按下及松开以覆盖 GUI 路由。</summary>
    private async Task Click(Control control, MouseButton button = MouseButton.Left, bool ctrl = false, Action? checkBeforeFrames = null)
    {
        var point = control.GetGlobalRect().GetCenter();
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
        Mouse(point, button, true, ctrl);
        Mouse(point, button, false, ctrl);
        checkBeforeFrames?.Invoke();
        await Frames();
    }

    /// <summary>构造独立鼠标事件，不直接调用业务回调。</summary>
    private void Mouse(Vector2 position, MouseButton button, bool pressed, bool ctrl = false)
    {
        using var input = new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button,
            Pressed = pressed, CtrlPressed = ctrl, ButtonMask = pressed ? button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right : 0 };
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
