using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Services;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>以真实鼠标按钮验收 openspec/specs/server-v2-account 的独立页面、草稿和摘要重启恢复。</summary>
public partial class ServerV2RoleManagementSmoke : Node
{
    /// <summary>隔离设置与摘要，完整场景退出后才重启并最终清理。</summary>
    public override async void _Ready()
    {
        GetViewport().GuiEmbedSubwindows = true;
        var directory = Path.Combine(Path.GetTempPath(), "rpg-v2-role-ui-" + Guid.NewGuid());
        ServerV2RoleManagement? main = null;
        try
        {
            main = Start(directory);
            await Frames();
            GameServices.Env.ServerFlushIntervalSeconds = 0;
            var password = (LineEdit)main.FindChild("Password", true, false);
            password.Text = "wrong";
            await Click(LoginButton(main));
            await Until(() => !GameServices.Session.AccountRequestPending);
            Check(!GameServices.Session.IsLoggedIn && !LoginButton(main).Disabled, "错误凭据未恢复登录按钮");
            password.Text = "demo";
            await Click(LoginButton(main));
            await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
            Check(GameServices.Session.Roles.Count == 0, "首次登录不为空");
            await Click(Button(main, "OpenCreateRole"));
            await Frames();
            var page = (RoleCreateControl)main.FindChild("CreateRolePage", true, false);
            await Click(Button(page, "GenderFemale"));
            await Click(Button(page, "HeadChoice17"));
            await Click(Button(page, "SubmitCreate"));
            var name = (LineEdit)page.FindChild("RoleName", true, false);
            await Click(Button(page, "ConfirmCreate"));
            await Until(() => name.Editable && ((Label)page.FindChild("CreateFeedback", true, false)).Text.Length != 0);
            Check(((ConfirmationDialog)page.FindChild("RoleNameDialog", true, false)).Visible, "失败关闭了命名窗口");
            name.Text = "V2 草稿角色";
            await Click(Button(page, "ConfirmCreate"));
            Check(GameServices.Session.AccountRequestPending && !name.Editable, "提交期间未阻断重复请求");
            await Until(() => main.FindChild("RoleSelectionPage", true, false) is not null);
            var role = GameServices.Session.Roles.Single();
            Check(role.Name == "V2 草稿角色" && role.Gender == RoleGender.Female && role.HeadId == 17, "外观草稿没有进入摘要");
            Check(Button(main, "EnterRole").Disabled && Button(main, "EnterRole").Text == "进入世界待迁移", "V2 允许进入未迁移世界");
            Check(main.FindChild("PreviewActor", true, false) is not null && !GameServices.World.HasWorld, "未显示预览或初始化了世界");
            Check(!Directory.Exists(Path.Combine(directory, "server", "v2")), "禁用周期后仍提前保存了摘要");
            await Click(Button(main, $"DeleteRole{role.RoleId}"));
            await Frames();
            var dialog = (ConfirmationDialog)main.FindChild("ConfirmDeleteRole", true, false);
            await Click(dialog.GetCancelButton());
            await Frames();
            Check(GameServices.Session.Roles.Count == 1, "取消删除仍发送请求");
            await Click(Button(main, $"DeleteRole{role.RoleId}"));
            await Frames();
            dialog = (ConfirmationDialog)main.FindChild("ConfirmDeleteRole", true, false);
            await Click(dialog.GetOkButton());
            await Until(() => GameServices.Session.Roles.Count == 0);
            main.Free();
            main = null;
            await Frames();
            var root = Path.Combine(directory, "server", "v2");
            Check(Directory.GetFiles(root).Select(Path.GetFileName).SequenceEqual(new[] { "demo.role_summary.json" }) &&
                Directory.GetDirectories(root).Length == 0, "关闭未仅保存摘要");
            main = Start(directory);
            await Frames();
            await Click(LoginButton(main));
            await Until(() => GameServices.Session.IsLoggedIn);
            Check(GameServices.Session.Roles.Count == 0, "重启恢复了删除角色");
            await Click(Button(main, "OpenCreateRole"));
            await Frames();
            page = (RoleCreateControl)main.FindChild("CreateRolePage", true, false);
            await Click(Button(page, "SubmitCreate"));
            ((LineEdit)page.FindChild("RoleName", true, false)).Text = "重启后角色";
            await Click(Button(page, "ConfirmCreate"));
            await Until(() => GameServices.Session.Roles.Count == 1);
            Check(GameServices.Session.Roles[0].RoleId == role.RoleId + 1, "墓碑编号被重用");
            await Click(Button(main, "ReturnLogin"));
            await Frames();
            Check(!GameServices.Session.IsLoggedIn && GameServices.Session.Roles.Count == 0, "返回登录未清理缓存");
            GD.Print("Server V2 role management smoke passed: real buttons, drafts, preview, cancellation, tombstones and restart.");
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            main?.Free();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
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
