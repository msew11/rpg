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

/// <summary>验证 openspec/specs/role-management、server-write-back 与 client-ui 的角色界面、背景音乐及离角落地。</summary>
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
            // client-ui：登录与角色页面循环使用 M001，并走 BGM 总线。
            var bgm = (BgmPlayer)main.FindChild("Bgm", true, false);
            var music = bgm.GetNode<AudioStreamPlayer>("Music");
            Check(bgm.CurrentTrackId == "M001" && music.Bus == "BGM" &&
                music.Stream is AudioStreamOggVorbis { Loop: true } &&
                music.Stream.ResourcePath.EndsWith("M001.ogg"), "登录页没有循环播放 M001 或没有使用 BGM 总线");
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(bgm.CurrentTrackId == "M001", "选角页没有保持 M001");
            GameServices.Env.ServerFlushIntervalSeconds = 0;
            var selection = (RoleSelectionControl)main.FindChild("RoleSelectionPage", true, false);
            AssertLayout(selection, "ReturnLogin");
            Check(GameServices.Session.Roles.Count == 0, "全新存档登录生成了角色");
            Check(selection.FindChild("PreviewPortrait", true, false) is AvatarControl { Visible: false },
                "空角色列表显示了虚构头像");
            Check(main.FindChild("OpenCreateRole", true, false) is Button { Disabled: false }, "首次空列表无法创建");
            Press(main, "OpenCreateRole");
            await Frames();
            Check(bgm.CurrentTrackId == "M001", "角色创建页没有保持 M001");
            var page = main.FindChild("CreateRolePage", true, false) as RoleCreateControl
                ?? throw new InvalidOperationException("创建页面未显示");
            AssertLayout(page, "CancelCreate");
            var actor = (ActorView)page.FindChild("PreviewActor", true, false);
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            var head = actor.GetNode<Sprite2D>("Facing/Head");
            var preview = (RolePreviewControl)page.FindChild("RolePreview", true, false);
            Check(actor.Scale == Vector2.One * 2.4f, "装备预览比例影响了创角预览");
            foreach (var buttonName in new[] { "RotateClockwise", "RotateCounterclockwise" })
            {
                var button = (Button)preview.FindChild(buttonName, true, false);
                Check(button.Text.Length == 0 && button.Icon is not null, "创角旋转按钮仍使用文字");
            }
            var portrait = (AvatarControl)page.FindChild("PreviewPortrait", true, false);
            Check(ReferenceEquals(head.Texture, GameServices.Animation.GetHead(RoleGender.Male, 9).Frames[4]),
                "首次预览没有使用首个可选头型");
            // role-management：圆形头像与 HUD 使用同一头型素材，且位于人物左上方。
            Check(portrait.Visible && portrait.Portrait?.ResourcePath.EndsWith("DThed011_01.png") == true &&
                !portrait.GetNode<Label>("LevelLabel").Visible &&
                portrait.GetGlobalRect().End.X < actor.GlobalPosition.X &&
                portrait.GetGlobalRect().End.Y < actor.GlobalPosition.Y,
                "初始头像缺失、含等级或位置偏离人物左上方");
            Check(!GameServices.World.HasWorld, "预览创建了 ECS 世界");
            var original = (body.Animation, actor.GetNode<Node2D>("Facing").Scale);
            for (var i = 0; i < 8; i++) Press(page, "RotateClockwise");
            Check(original == (body.Animation, actor.GetNode<Node2D>("Facing").Scale), "顺时针八次没有回环");
            Press(page, "RotateCounterclockwise");
            Press(page, "RotateClockwise");
            Check(original == (body.Animation, actor.GetNode<Node2D>("Facing").Scale), "顺逆旋转未抵消");
            Check(page.FindChildren("*", "OptionButton", true, false).Count == 0, "创建页仍使用下拉菜单");
            var choices = (GridContainer)page.FindChild("HeadGrid", true, false);
            Check(choices.GetChildCount() == 64 &&
                page.FindChild("HeadChoice1", true, false) is null &&
                page.FindChild("HeadChoice73", true, false) is null,
                "创建页未收敛到参考页的 64 款头型");
            Check(((Button)page.FindChild("HeadChoice9", true, false)).ButtonPressed,
                "默认没有选中首款对应头像的头型");
            var firstChoiceImage = ((Button)page.FindChild("HeadChoice9", true, false))
                .FindChildren("*", "TextureRect", true, false).OfType<TextureRect>().Single();
            Check(firstChoiceImage.Texture?.ResourcePath.EndsWith("DThed011_01.png") == true,
                "创角缩略图未使用对应的 DT 头像");
            Press(page, "HeadChoice17");
            Check(portrait.Portrait?.ResourcePath.EndsWith("DThed021_01.png") == true,
                "切换男性头型未更新圆形头像");
            Press(page, "RotateClockwise");
            var maleDirection = preview.Direction;
            Press(page, "GenderFemale");
            await Frame();
            Check(preview.Direction == 2, "女性继承了男性的朝向");
            Check(ReferenceEquals(head.Texture, GameServices.Animation.GetHead(RoleGender.Female, 9).Frames[4]), "女性继承了男性头型");
            Check(portrait.Portrait?.ResourcePath.EndsWith("DThed011_51.png") == true,
                "切换女性未更新圆形头像");
            Press(page, "HeadChoice72");
            Check(portrait.Portrait?.ResourcePath.EndsWith("DThed088_51.png") == true,
                "末款头型未显示对应的 DT 头像");
            Press(page, "RotateCounterclockwise");
            var femaleDirection = preview.Direction;
            Press(page, "GenderMale");
            await Frame();
            Check(preview.Direction == maleDirection, "男性朝向未恢复");
            Check(((Button)page.FindChild("HeadChoice17", true, false)).ButtonPressed, "男性头型被女性修改覆盖");
            Press(page, "GenderFemale");
            await Frame();
            Check(preview.Direction == femaleDirection, "女性朝向未恢复");
            Check(((Button)page.FindChild("HeadChoice72", true, false)).ButtonPressed, "女性头型未恢复");
            // 取消命名后切换性别，两个名字草稿也必须独立。
            Press(page, "SubmitCreate");
            var draftDialog = (ConfirmationDialog)page.FindChild("RoleNameDialog", true, false);
            var draftName = (LineEdit)page.FindChild("RoleName", true, false);
            draftName.Text = "女性草稿";
            draftDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            draftDialog.Hide();
            Press(page, "GenderMale");
            Press(page, "SubmitCreate");
            Check(draftName.Text == "", "男性继承了女性的名字草稿");
            draftName.Text = "男性草稿";
            draftDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            draftDialog.Hide();
            Press(page, "GenderFemale");
            Press(page, "SubmitCreate");
            Check(draftName.Text == "女性草稿", "女性名字草稿被男性修改覆盖");
            draftName.Text = "";
            draftDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            draftDialog.Hide();
            await Capture("role-create-preview.png");
            GetWindow().Size = new Vector2I(800, 600);
            await Frames();
            AssertLayout(page, "CancelCreate");
            var scroll = (ScrollContainer)page.FindChild("HeadList", true, false);
            var last = (Button)page.FindChild("HeadChoice72", true, false);
            scroll.EnsureControlVisible(last);
            await Frames();
            Check(scroll.GetGlobalRect().Intersects(last.GetGlobalRect()), "末尾头型无法通过滚动到达");
            await Capture("role-create-small.png");
            GetWindow().Size = new Vector2I(1280, 720);
            await Frames();
            Press(page, "SubmitCreate");
            var nameDialog = (ConfirmationDialog)page.FindChild("RoleNameDialog", true, false);
            Check(nameDialog.Visible && GameServices.Session.Roles.Count == 0, "提示命名时已发送创建请求");
            nameDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            nameDialog.Hide();
            Check(GameServices.Session.Roles.Count == 0, "取消命名仍创建了角色");
            Press(page, "SubmitCreate");
            Press(page, "ConfirmCreate");
            var name = (LineEdit)page.FindChild("RoleName", true, false);
            Check(!name.Editable, "请求期间输入未禁用");
            await Frames();
            Check(name.Editable && nameDialog.Visible, "创建失败未保留命名窗口或恢复操作");
            Check(((Label)page.FindChild("CreateFeedback", true, false)).Text.Length > 0, "创建失败未显示原因");
            name.Text = "分层预览验收";
            await Capture("role-name-prompt.png");
            Press(page, "ConfirmCreate");
            await Frames();
            var role = GameServices.Session.Roles.Single(value => value.Name == "分层预览验收");
            Check(role.HeadId == 72 && role.Gender == RoleGender.Female, "创建没有保存草稿外观");
            selection = (RoleSelectionControl)main.FindChild("RoleSelectionPage", true, false);
            Press(main, $"SelectRole{role.RoleId}");
            var selectedActor = (ActorView)selection.FindChild("PreviewActor", true, false);
            var selectedPortrait = (AvatarControl)selection.FindChild("PreviewPortrait", true, false);
            Check(ReferenceEquals(selectedActor.GetNode<Sprite2D>("Facing/Head").Texture,
                GameServices.Animation.GetHead(RoleGender.Female, 72).Frames[4]), "选角未展示新创建的角色");
            Check(selectedPortrait.Visible && selectedPortrait.Portrait?.ResourcePath.EndsWith("DThed088_51.png") == true,
                "选角头像与角色头型不一致");
            Press(selection, "RotateClockwise");
            await Capture("role-selection-preview.png");
            Press(main, $"DeleteRole{role.RoleId}");
            await Frame();
            var dialog = (ConfirmationDialog)main.FindChild("ConfirmDeleteRole", true, false);
            dialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            await Frame();
            Check(GameServices.Session.Roles.Count == 1, "取消确认仍删除了角色");
            Press(main, $"SelectRole{role.RoleId}");
            Check(!GameServices.World.HasWorld, "预览角色时创建了世界");
            Press(main, "EnterRole");
            await Frames();
            Check(GameServices.World.MapConfig.BgmTrackId == "M015a" &&
                bgm.CurrentTrackId == "M015a" && music.Stream is AudioStreamOggVorbis { Loop: true } &&
                music.Stream.ResourcePath.EndsWith("M015a.ogg"), "地图 1 没有按配置循环播放 M015a");
            Check(GameServices.World.ActorState?.HeadId == 72, "选角没有把头型交给世界");
            var rolePath = Path.Combine(directory, "server", $"{role.RoleId}.role.json");
            Check(!File.Exists(rolePath), "禁用定时检测时角色已提前落地");
            var hud = (GameHud)main.FindChild("GameHud", true, false);
            hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "系统")
                .EmitSignal(Button.SignalName.Pressed);
            hud.GetNode("SystemMenuOverlay/SystemMenu").FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            await Frame();
            Check(bgm.CurrentTrackId == "M001", "返回选角后没有恢复 M001");
            Check(File.Exists(rolePath), "返回角色选择没有在清理会话前落地角色文件");
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

    /// <summary>核对右栏、中央操作与右下角返回的位置，防止小窗口遮挡。</summary>
    private static void AssertLayout(RoleScreenControl page, string backName)
    {
        var right = page.GetNode<PanelContainer>("RightPanel");
        var preview = page.GetNode<RolePreviewControl>("RolePreview");
        var back = (Button)page.FindChild(backName, true, false);
        Check(right.Position.X > page.Size.X / 2, "选项未位于屏幕右侧");
        Check(preview.GetRect().End.X <= right.Position.X, "预览操作与右栏重叠");
        Check(back.Position.Y >= right.GetRect().End.Y && back.GetRect().End.Y <= page.Size.Y, "返回按钮不在右下角");
        Check(preview.GetRect().End.Y <= page.Size.Y, "中央操作超出窗口");
    }

    /// <summary>可选使用实际渲染输出选角、创建与命名页面供目视验收。</summary>
    private async Task Capture(string file)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture-role")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://build/" + file));
    }

    /// <summary>模拟按钮点击，禁用状态不允许提交操作。</summary>
    private static void Press(Node root, string name)
    {
        var button = root.FindChild(name, true, false) as Button ?? throw new InvalidOperationException($"找不到按钮：{name}");
        Check(!button.Disabled, $"按钮未恢复：{name}");
        if (button.ToggleMode) button.ButtonPressed = true;
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
