using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Config;
using Rpg.Net;
using Rpg.Services;
using Rpg.ServerV2.Hosting;
using Rpg.Settings;
using Rpg.UI;

namespace Rpg.Tests;

/// <summary>server-v2-movement 和 server-v2-world-entry 双进程验收：互见移动、返回选角移除及重新进入。</summary>
public partial class ServerV2MultiplayerSmoke : MainController
{
    /// <summary>协调目录，和真实用户目录隔离。</summary>
    private string Root => OS.GetCmdlineUserArgs()[1];
    /// <summary>本进程是否运行服务器。</summary>
    private bool Host => OS.GetCmdlineUserArgs().Contains("--host");
    /// <summary>子进程使用真实TCP装配，仅替换本地路径。</summary>
    protected override IDisposable InitializeServices()
    {
        var env = new EnvService(Path.Combine(Root, Host ? "host-settings" : "client-settings"), Path.Combine(Root, "server"));
        var events = new EventBusService(); var config = new ConfigService(env); var log = new LogService(_ => { });
        var setting = new SettingService(env, log); var server = new MockServerService(env, config);
        var net = new NetService(server, events, log);
        return GameServicesTestFactory.Register(env, events, config, setting, server, net);
    }
    /// <summary>通过同一UI事件提交登录和角色创建，退出前验证远端ActorView。</summary>
    public override async void _Ready()
    {
        base._Ready();
        try
        {
            if (!Host) await Until(() => File.Exists(Path.Combine(Root, "host-ready")));
            ((LineEdit)FindChild("ServerPort", true, false)).Text = OS.GetCmdlineUserArgs()[2];
            if (!Host)
            {
                var mode = (OptionButton)FindChild("ConnectionMode", true, false); mode.Select(1); mode.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
            }
            FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
            await Until(() => GameServices.Session.IsLoggedIn);
            if (Host) File.WriteAllText(Path.Combine(Root, "host-ready"), "ready");
            else if (GameServices.MockServer.Server is not null) throw new InvalidOperationException("纯客户端创建了服务器");
            ((Button)FindChild("OpenCreateRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames(); ((Button)FindChild("SubmitCreate", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((LineEdit)FindChild("RoleName", true, false)).Text = Host ? "服务器玩家" : "客户端玩家";
            ((Button)FindChild("ConfirmCreate", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Until(() => FindChild("RoleSelectionPage", true, false) is not null && GameServices.Session.Roles.Count == 1);
            ((Button)FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Until(() => GameServices.Session.CanMove && GameServices.World.HasRole && GameServices.Session.RemoteRoles.Count == 1);
            await Until(() => GameServices.World.RemoteActors.Count == 1);
            var initial = GameServices.World.RemoteActors.Single().Position;
            var own = GameServices.World.RolePosition;
            using var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
            if (!GameServices.World.HandleInput(click, own + new Vector2(400, 300))) throw new InvalidOperationException("点击预测未接纳");
            await Until(() => GameServices.World.RolePosition.DistanceTo(own) > 3 && GameServices.World.RemoteActors.Single().Position.DistanceTo(initial) > 3);
            var remoteId = GameServices.World.RemoteActors.Single().RoleId;
            if (FindChild("RemoteRole" + remoteId, true, false) is null) throw new InvalidOperationException("远端ActorView未呈现");
            var name = Host ? "host" : "client"; var other = Host ? "client" : "host";
            File.WriteAllText(Path.Combine(Root, name + "-moved"), "互见和移动通过");
            await Until(() => File.Exists(Path.Combine(Root, other + "-moved")));
            if (Host)
            {
                await Until(() => File.Exists(Path.Combine(Root, "client-in-roles")));
                await Until(() => GameServices.Session.RemoteRoles.Count == 0 && GameServices.World.RemoteActors.Count == 0 &&
                    FindChild("RemoteRole" + remoteId, true, false) is null);
                File.WriteAllText(Path.Combine(Root, "host-saw-leave"), "角色已从缓存、实体和节点移除");
                await Until(() => File.Exists(Path.Combine(Root, "client-reentered")));
                await Until(() => GameServices.Session.RemoteRoles.ContainsKey(remoteId) && GameServices.World.RemoteActors.Count == 1 &&
                    FindChild("RemoteRole" + remoteId, true, false) is not null);
                File.WriteAllText(Path.Combine(Root, "host-saw-reentry"), "角色重新出现");
            }
            else
            {
                PressTextButton("系统"); await Frames(); PressTextButton("返回角色选择");
                await Until(() => FindChild("RoleSelectionPage", true, false) is not null);
                if (!GameServices.Session.IsLoggedIn || GameServices.Session.HasRole || GameServices.World.HasWorld)
                    throw new InvalidOperationException("返回选角未保留账号或清理世界");
                File.WriteAllText(Path.Combine(Root, "client-in-roles"), "已返回角色选择且保持TCP连接");
                await Until(() => File.Exists(Path.Combine(Root, "host-saw-leave")));
                ((Button)FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
                await Until(() => GameServices.Session.CanMove && GameServices.World.RemoteActors.Count == 1 &&
                    FindChild("RemoteRole" + remoteId, true, false) is not null);
                File.WriteAllText(Path.Combine(Root, "client-reentered"), "同连接重新进入成功");
                await Until(() => File.Exists(Path.Combine(Root, "host-saw-reentry")));
            }
            File.WriteAllText(Path.Combine(Root, name + "-done"), "互见、移动、返回选角消失及重新进入通过");
            await Until(() => File.Exists(Path.Combine(Root, other + "-done")));
            GD.Print(name + " multiplayer process passed"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    /// <summary>调用当前可见且唯一的菜单按钮，走正式页面返回流程。</summary>
    private void PressTextButton(string text) => FindChildren("*", "Button", true, false).OfType<Button>()
        .Single(button => button.Text == text && button.IsVisibleInTree()).EmitSignal(Button.SignalName.Pressed);
    /// <summary>固定帧等待，保留网络和人物绘制推进。</summary>
    private async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    /// <summary>每项交互均有明确超时。</summary>
    private async Task Until(Func<bool> done) { var limit = DateTime.UtcNow.AddSeconds(20); while (!done()) { if (DateTime.UtcNow > limit) throw new TimeoutException("双进程验收等待超时"); await Frames(); } }
}
