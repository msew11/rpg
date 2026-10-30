using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>以真实按钮验收 server-v2-world-entry 的进入、初始展示、只读窗口、切角和重启。</summary>
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
            Check(!((LineEdit)main.FindChild("CommandInput", true, false)).Editable && Button(main, "SendCommand").Disabled, "GM 仍可提交");
            var position = GameServices.World.RolePosition;
            await ClickAt(new Vector2(750, 230)); await Frames();
            Check(GameServices.World.RolePosition != position || GameServices.World.RoleTarget is not null, "首帧后未开放预测移动");
            await Click(TextButton(main, "背包")); await Frames();
            var bag = (BagControl)main.FindChild("背包", true, false);
            Check(bag.Visible && bag.CreateItemDrag(0).VariantType == Variant.Type.Nil, "只读背包不可用或允许拖动物品");
            await Click(TextButton(main, "背包"));
            await Click(TextButton(main, "角色")); await Frames();
            Check(((Label)main.FindChild("EquipmentStatus", true, false)).Text.Contains("只读"), "装备窗口缺少只读提示");
            await Click(TextButton(main, "角色"));
            await ReturnToRoles(main);
            Check(!GameServices.World.HasWorld && !GameServices.Session.HasRole && !GameServices.Session.HasMap, "返回未清世界缓存");
            await Click(Button(main, $"SelectRole{female}")); await Click(Button(main, "EnterRole"));
            await Until(() => main.FindChild("GameHud", true, false) is not null);
            Check(GameServices.Session.RoleId == female && GameServices.Session.Role.Position.X == 16144, "切角没有使用女角色存档");
            await ReturnToRoles(main);
            main.Free(); main = null; await Frames();
            var root = Path.Combine(directory, "server", "v2");
            Check(Directory.GetFiles(root).Length == 9 && Directory.GetDirectories(root).Length == 0, "三表和编号没有完整落地或只读空地图产生文件");
            var repo = new Rpg.ServerV2.Modules.Role.Storage.RepoRole(root);
            var stored = await repo.LoadAsync(female);
            using (var seedEnv = new Rpg.Config.EnvService(directory, Path.Combine(directory, "server")))
            using (var seedConfig = new Rpg.Config.ConfigService(seedEnv))
            {
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
            Check(bag.FindChild("PickedItemPreview", true, false) is null && GameServices.Session.Bag.Items.Count == 1, "只读物品仍可拿起");
            await Click(TextButton(main, "背包")); await Click(TextButton(main, "角色")); await Frames();
            await ClickAt(((Control)main.FindChild("EquipmentMainHand", true, false)).GetGlobalRect().GetCenter());
            Check(bag.FindChild("PickedItemPreview", true, false) is null && GameServices.Session.Equipment.Items.Count == 1, "只读装备仍可卸下");
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
            GD.Print("Server V2 world entry smoke passed: real buttons, map, character, HUD, read-only bag/equipment, switch and restart.");
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
    /// <summary>真实地图坐标点击用于验证只读输入不能移动。</summary>
    private async Task ClickAt(Vector2 point)
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
                Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
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
    private async Task Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!done() && DateTime.UtcNow < deadline) await Frames();
        Check(done(), "等待 V2 页面或响应超时");
        await Frames();
    }
    /// <summary>让网络固定帧、布局和释放队列完成。</summary>
    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    /// <summary>以退出码和具体原因报告失败。</summary>
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
