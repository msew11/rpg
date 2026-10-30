using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>以真实按钮验收 server-v2-world-entry 的进入、初始展示、物品窗口、切角和重启。</summary>
public partial class ServerV2WorldEntrySmoke : Node
{
    /// <summary>操作完整 V2 服务组和真实页面，所有数据使用临时目录。</summary>
    public override async void _Ready()
    {
        GetViewport().GuiEmbedSubwindows = true;
        var directory = Path.Combine(Path.GetTempPath(), "rpg-v2-world-ui-" + Guid.NewGuid());
        ServerV2RoleManagement? main = null;
        try
        {
            main = Start(directory); await Frames();
            GameServices.Env.ServerFlushIntervalSeconds = 0;
            await Click(LoginButton(main));
            await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
            await Create(main, "进入男角色", false);
            var male = GameServices.Session.Roles.Single().RoleId;
            await Create(main, "进入女角色", true);
            var female = GameServices.Session.Roles.Max(role => role.RoleId);
            await Click(Button(main, $"SelectRole{male}"));
            await Click(Button(main, "EnterRole"));
            Check(Button(main, "EnterRole").Disabled, "进入期间未阻断重复操作");
            await Until(() => main.FindChild("GameHud", true, false) is not null);
            Check(GameServices.World.HasWorld && GameServices.World.HasRole && GameServices.Session.HasMap, "世界缓存和实体未完整建立");
            Check(main.FindChild("MapView", true, false) is not null && main.FindChild("ActorView", true, false) is not null, "地图或人物缺失");
            Check(GameServices.Session.Role.Health == 100 && GameServices.Session.Role.Mana == 60, "初始 HUD 数值无效");
            Check(((LineEdit)main.FindChild("CommandInput", true, false)).Editable && !Button(main, "SendCommand").Disabled, "GM 未开放");
            ((LineEdit)main.FindChild("CommandInput", true, false)).Text = "/item 1012";
            await Click(Button(main, "SendCommand"));
            await Until(() => GameServices.Session.Bag.Items.Count == 1);
            var position = GameServices.World.RolePosition;
            await ClickAt(new Vector2(750, 230)); await Frames();
            Check(GameServices.World.RolePosition != position || GameServices.World.RoleTarget is not null, "首帧后未开放预测移动");
            await Click(TextButton(main, "背包")); await Frames();
            var bag = (BagControl)main.FindChild("背包", true, false);
            Check(bag.Visible && bag.CreateItemDrag(0).VariantType != Variant.Type.Nil, "背包物品不能操作");
            await Until(() => GameServices.World.RoleTarget is null);
            await ClickAt(((Control)bag.FindChild("Slot0", true, false)).GetGlobalRect().GetCenter());
            Check(bag.FindChild("PickedItemPreview", true, false) is not null, "丢弃前未拿起背包物品");
            await ClickAt(new Vector2(20, 220));
            await Until(() => GameServices.Session.Bag.Items.Count == 0 && GameServices.Session.Map!.Objects.Count == 1);
            await Click(TextButton(main, "背包"));
            await Until(() => main.FindChildren("GroundItemIcon", "TextureRect", true, false).Count == 1);
            var groundIcon = main.FindChildren("GroundItemIcon", "TextureRect", true, false).OfType<TextureRect>().Single();
            bool? pickupResult = null;
            using (GameServices.EventBus.Subscribe<Rpg.ProtocolV2.PickupItemResultNotice>(notice => pickupResult = notice.Success))
            {
                await ClickAt(groundIcon.GetGlobalTransformWithCanvas() * new Vector2(groundIcon.Size.X / 2, groundIcon.Size.Y / 2));
                await Until(() => pickupResult is not null && GameServices.Session.Map!.Objects.Count == 0);
            }
            Check(pickupResult == true && GameServices.Session.Bag.Items.Count == 1, "V2 鼠标走近拾取失败");
            await Click(TextButton(main, "角色")); await Frames();
            Check(!((Label)main.FindChild("EquipmentStatus", true, false)).Visible, "装备窗口仍为只读");
            await Click(TextButton(main, "角色"));
            await ReturnToRoles(main);
            Check(!GameServices.World.HasWorld && !GameServices.Session.HasRole && !GameServices.Session.HasMap, "返回未清世界缓存");
            await Click(Button(main, $"SelectRole{female}")); await Click(Button(main, "EnterRole"));
            await Until(() => main.FindChild("GameHud", true, false) is not null);
            Check(GameServices.Session.RoleId == female && GameServices.Session.Role.Position.X == 16144, "切角没有使用女角色存档");
            await ReturnToRoles(main);
            main.Free(); main = null; await Frames();
            var root = Path.Combine(directory, "server", "v2");
            Check(Directory.GetFiles(root).Length == 9, "三表、摘要和编号没有完整落地");
            var repo = new Rpg.ServerV2.Modules.Role.Storage.RepoRole(root);
            var stored = await repo.LoadAsync(female);
            using (var seedEnv = new Rpg.Config.EnvService(directory, Path.Combine(directory, "server")))
            using (var seedConfig = new Rpg.Config.ConfigService(seedEnv))
            {
                var map = seedConfig.Map.Items[1];
                var mapRepo = new Rpg.ServerV2.Modules.World.Storage.RepoWorld(root);
                Check((await mapRepo.LoadAsync(new(1, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles))).Objects.IsEmpty,
                    "重启仍恢复了已经拾取的地面物品");
                var gear = seedConfig.Item.Items.Values.First(item => item.MaxStack == 1 &&
                    Rpg.Utils.EquipmentSlotUtil.TryGetSlot(item.Category, item.SubCategory, out var slot) && slot == Rpg.Const.EquipmentSlot.MainHand);
                await repo.SaveAsync(female, stored with { Role = stored.Role with { Level = 3, NextInstanceId = 3 },
                    Items = [new(1, gear.Id, Rpg.Const.EquipmentSlot.MainHand), new(2, gear.Id, 0, 1)],
                    Equipments = [new() { InstanceId = 1 }, new() { InstanceId = 2 }] }, Rpg.ServerV2.Modules.Role.Data.DataRoleTables.All);
            }
            main = Start(directory); await Frames();
            await Click(LoginButton(main));
            await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
            Check(GameServices.Session.Roles.Count == 2, "重启摘要没有恢复");
            await Click(Button(main, $"SelectRole{female}")); await Click(Button(main, "EnterRole"));
            await Until(() => main.FindChild("GameHud", true, false) is not null);
            Check(GameServices.Session.RoleId == female && GameServices.Session.Role.Position.X == 16144, "重启进入没有恢复角色");
            Check(GameServices.Session.Bag.Items.Count == 1 && GameServices.Session.Equipment.Items.Count == 1, "已有背包装备未加载");
            await Click(TextButton(main, "背包")); await Frames();
            bag = (BagControl)main.FindChild("背包", true, false);
            await ClickAt(((Control)bag.FindChild("Slot0", true, false)).GetGlobalRect().GetCenter());
            Check(bag.FindChild("PickedItemPreview", true, false) is not null, "背包物品未拿起");
            await ClickAt(((Control)bag.FindChild("Slot1", true, false)).GetGlobalRect().GetCenter());
            await Until(() => GameServices.Session.Bag.Items.Single().SlotIndex == 1);
            await Click(TextButton(main, "背包")); await Click(TextButton(main, "角色")); await Frames();
            await ClickAt(((Control)main.FindChild("EquipmentMainHand", true, false)).GetGlobalRect().GetCenter(), ctrl: true);
            await Until(() => GameServices.Session.Equipment.Items.Count == 0);
            Check(GameServices.Session.Bag.Items.Count == 2, "卸下未原子同步背包");
            await Click(TextButton(main, "角色"));
            var summaryFile = Path.Combine(root, "demo1.role_summary.json");
            var backup = Path.Combine(directory, "summary-backup.json");
            File.Move(summaryFile, backup);
            Directory.CreateDirectory(summaryFile);
            var printErrors = Engine.PrintErrorMessages;
            try
            {
                // 此处主动触发已知保存故障，HUD 是断言入口，避免预期诊断使外层烟测误报。
                Engine.PrintErrorMessages = false;
                await Click(TextButton(main, "系统")); await Click(TextButton(main, "返回角色选择")); await Frames();
                Check(GameServices.World.HasWorld && GameServices.Session.HasRole && main.FindChild("GameHud", true, false) is not null,
                    "写回失败仍离开了页面");
                Check(((Label)main.FindChild("NetworkError", true, false)).Text.Length > 0, "写回失败没有 HUD 反馈");
                main.Notification((int)NotificationWMCloseRequest);
                Check(!GetTree().AutoAcceptQuit && GameServices.World.HasWorld, "系统关闭未保留失败页面");
            }
            finally
            {
                Engine.PrintErrorMessages = printErrors;
                Directory.Delete(summaryFile); File.Move(backup, summaryFile);
            }
            await ReturnToRoles(main);
            Check(GameServices.Session.Roles.Single(role => role.RoleId == female).Level == 3, "实际状态没有在进入时同步摘要");
            await Click(Button(main, $"SelectRole{female}")); await Click(Button(main, "EnterRole"));
            await Until(() => main.FindChild("GameHud", true, false) is not null);
            await Click(TextButton(main, "系统")); await Click(TextButton(main, "返回登陆")); await Frames();
            Check(!GameServices.Session.IsLoggedIn && !GameServices.World.HasWorld, "返回登录未清理世界及账号");
            GD.Print("Server V2 world entry smoke passed: real buttons, map, character, HUD, writable GM/bag/equipment, switch and restart.");
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { main?.Free(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    /// <summary>通过现有外观、命名及确认按钮建立摘要。</summary>
    private async Task Create(Node main, string name, bool female)
    {
        await Click(Button(main, "OpenCreateRole")); await Frames();
        var page = (RoleCreateControl)main.FindChild("CreateRolePage", true, false);
        await Click(Button(page, female ? "GenderFemale" : "GenderMale"));
        await Click(Button(page, "HeadChoice17")); await Click(Button(page, "SubmitCreate"));
        ((LineEdit)page.FindChild("RoleName", true, false)).Text = name;
        await Click(Button(page, "ConfirmCreate"));
        await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
    }
    /// <summary>返回流程必须使用 HUD 的系统菜单真实按钮。</summary>
    private async Task ReturnToRoles(Node main)
    {
        await Click(TextButton(main, "系统")); await Click(TextButton(main, "返回角色选择"));
        await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
    }
    /// <summary>选择当前可见且文案唯一的菜单或窗口按钮。</summary>
    private static Button TextButton(Node root, string text) => root.FindChildren("*", "Button", true, false)
        .OfType<Button>().Single(button => button.Text == text && button.IsVisibleInTree());
    /// <summary>真实鼠标输入验证地图与物品交互。</summary>
    private async Task ClickAt(Vector2 point, bool ctrl = false)
    {
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
                Pressed = pressed, CtrlPressed = ctrl, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
            GetViewport().PushInput(input, true);
        }
        await Frames();
    }
    /// <summary>启动与手工验收相同的控制器，只替换存档目录。</summary>
    private ServerV2RoleManagement Start(string directory)
    {
        var main = new ServerV2RoleManagement { Name = "V2Client", TestDirectory = directory };
        AddChild(main);
        return main;
    }
    /// <summary>通过实际 GUI 视口注入按下和释放，窗口内按钮使用其自己的视口坐标。</summary>
    private async Task Click(Button button)
    {
        await Frames();
        Check(!button.Disabled, $"按钮被禁用：{button.Name}");
        var viewport = GetViewport();
        var point = button.GetGlobalRect().GetCenter();
        if (button.GetWindow() != GetWindow()) point += button.GetWindow().Position;
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        viewport.PushInput(motion, true);
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
            viewport.PushInput(input, true);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    /// <summary>找到命名按钮，缺失时报告明确的验收错误。</summary>
    private static Button Button(Node root, string name) => root.FindChild(name, true, false) as Button ?? throw new InvalidOperationException($"缺少按钮：{name}");
    /// <summary>复用默认登录页的实际登录按钮。</summary>
    private static Button LoginButton(Node root) => root.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录");
    /// <summary>等待真实异步回复，超时失败而不固定假设回复帧数。</summary>
    private async Task Until(Func<bool> done, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(done))] string condition = "")
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!done() && DateTime.UtcNow < deadline) await Frames();
        Check(done(), $"等待 V2 页面或响应超时：{condition}");
        await Frames();
    }
    /// <summary>显式推进不带后台时钟的进程内服务器，并等待网络、布局及释放队列。</summary>
    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (GameServices.IsInitialized && GameServices.MockServer.Server is { Port: 0 } server)
            await server.AdvanceAsync();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    /// <summary>以退出码和具体原因报告失败。</summary>
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
