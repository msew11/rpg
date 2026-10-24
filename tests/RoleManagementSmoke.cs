using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/role-management 的实际创建、旋转、删除 UI 及全部分层头型。</summary>
public partial class RoleManagementSmoke : Node
{
    /// <summary>隔离存档，自动操作真实按钮并以退出码报告验收结果。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-role-ui-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
            AddChild(main);
            await Frame();
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Press(main, "OpenCreateRole");
            await Frames();
            var page = main.FindChild("CreateRolePage", true, false) as RoleCreateControl
                ?? throw new InvalidOperationException("创建页面未显示");
            var actor = page.GetNode<ActorView>("RolePreview/PreviewActor");
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            var head = actor.GetNode<Sprite2D>("Facing/Head");
            Check(head.Texture is not null, "首次预览缺失头部");
            Check(!GameServices.World.HasWorld, "预览创建了 ECS 世界");
            var original = (body.Animation, actor.GetNode<Node2D>("Facing").Scale);
            for (var i = 0; i < 8; i++) Press(page, "RotateClockwise");
            Check(original == (body.Animation, actor.GetNode<Node2D>("Facing").Scale), "顺时针八次没有回环");
            Press(page, "RotateCounterclockwise");
            Press(page, "RotateClockwise");
            Check(original == (body.Animation, actor.GetNode<Node2D>("Facing").Scale), "顺逆旋转未抵消");
            var choices = (OptionButton)page.FindChild("HeadChoice", true, false);
            // OptionButton 位于头型行中，通过 FindChild 获取。
            Check(choices.ItemCount == 74, "创建页没有提供全部头型");
            choices.Select(73);
            choices.EmitSignal(OptionButton.SignalName.ItemSelected, 73L);
            var gender = page.GetNode<OptionButton>("GenderChoice");
            gender.Select(1);
            gender.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            Check(ReferenceEquals(head.Texture, GameServices.Animation.GetHead(RoleGender.Female, 74).Frames[4]), "性别或头型切换残留旧资源");
            Press(page, "SubmitCreate");
            Check(!page.GetNode<LineEdit>("RoleName").Editable, "请求期间输入未禁用");
            await Frames();
            Check(page.GetNode<LineEdit>("RoleName").Editable, "创建失败未恢复操作");
            Check(page.GetNode<Label>("CreateFeedback").Text.Length > 0, "创建失败未显示原因");
            page.GetNode<LineEdit>("RoleName").Text = "分层预览验收";
            if (OS.GetCmdlineUserArgs().Contains("--capture-role"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/role-create-preview.png"));
            }
            Press(page, "SubmitCreate");
            await Frames();
            var role = GameServices.Session.Roles.Single(value => value.Name == "分层预览验收");
            Check(role.HeadId == 74 && role.Gender == RoleGender.Female, "创建没有保存草稿外观");
            Press(main, $"DeleteRole{role.RoleId}");
            await Frame();
            var dialog = (ConfirmationDialog)main.FindChild("ConfirmDeleteRole", true, false);
            dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            await Frame();
            Check(GameServices.Session.Roles.Count == 3, "取消确认仍删除了角色");
            Press(main, $"SelectRole{role.RoleId}");
            await Frames();
            Check(GameServices.World.ActorState?.HeadId == 74, "选角没有把头型交给世界");
            var hud = (GameHud)main.FindChild("GameHud", true, false);
            hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "系统")
                .EmitSignal(Button.SignalName.Pressed);
            hud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            await Frame();
            foreach (var summary in GameServices.Session.Roles.ToArray())
            {
                Press(main, $"DeleteRole{summary.RoleId}");
                await Frame();
                dialog = (ConfirmationDialog)main.FindChild("ConfirmDeleteRole", true, false);
                dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
                await Frames();
            }
            Check(GameServices.Session.Roles.Count == 0, "界面无法删空角色列表");
            Check(main.FindChild("OpenCreateRole", true, false) is Button { Disabled: false }, "空列表无法继续创建");
            CheckAllLayers();
            main.QueueFree();
            await Frame();
            main = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Frame();
            GD.Print("Role management smoke passed: create/delete UI, rotation, 148 heads, all 13 motions.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree();
            await Frame();
            GetTree().Quit(1);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>遍历全部头型和动作，检查挂接、纹理与方向完整且身体资源始终复用。</summary>
    private void CheckAllLayers()
    {
        var actor = new ActorView();
        AddChild(actor);
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var motion in Enum.GetValues<RoleMotion>())
        {
            var animation = GameServices.Animation.Get(gender, motion);
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            var head = actor.GetNode<Sprite2D>("Facing/Head");
            Check(animation.HeadAttachments.Length == animation.Frames.GetFrameCount("dir0") * 5, "动作挂接点不完整");
            for (var id = 1; id <= 74; id++)
            for (var direction = 0; direction < 8; direction++)
            {
                var facing = IsometricProjection.ToLogical(Vector2.FromAngle(direction * Mathf.Pi / 4));
                actor.Present(new DataActorState(1, gender, Vector2.Zero, motion, facing, 0, id));
                Check(ReferenceEquals(body.SpriteFrames, animation.Frames), "换头复制了身体资源");
                var resource = GameServices.Animation.GetHead(gender, id);
                var (source, _) = AnimationService.ResolveDirection(facing);
                Check(ReferenceEquals(head.Texture, resource.Frames[source]), "头部方向与身体不同步");
                var count = animation.Frames.GetFrameCount(body.Animation);
                for (var frame = 0; frame < count; frame++)
                {
                    body.SetFrameAndProgress(frame, 0);
                    var actual = head.Position + resource.Anchors[source] + animation.Anchor;
                    Check(actual == animation.HeadAttachments[source * count + frame], "头部没有跟随身体逐帧挂接");
                }
            }
        }
        actor.QueueFree();
    }

    /// <summary>模拟按钮点击，禁用状态不允许提交操作。</summary>
    private static void Press(Node root, string name)
    {
        var button = root.FindChild(name, true, false) as Button ?? throw new InvalidOperationException($"找不到按钮：{name}");
        Check(!button.Disabled, $"按钮未恢复：{name}");
        button.EmitSignal(Button.SignalName.Pressed);
    }

    /// <summary>等待布局和释放队列完成一帧。</summary>
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    /// <summary>等待正式固定帧收发网络，再完成页面布局。</summary>
    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Frame();
    }

    /// <summary>以异常向场景运行器报告明确的验收失败。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
