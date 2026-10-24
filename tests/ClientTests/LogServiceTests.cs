using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Encodings.Web;
using Rpg.Config;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Services;
using Rpg.Settings;

namespace ClientTests;

/// <summary>验证 openspec/specs/console-logging 的局部颜色、实际收发与成功文件提交日志。</summary>
public sealed class LogServiceTests
{
    /// <summary>console-logging：默认关闭且不访问消息或解析日志 JSON，开关只保留在当前环境。</summary>
    [Fact]
    public void EnvironmentToggleSkipsFormattingAndResetsWithNewEnvironment()
    {
        using var env = new EnvService("");
        var lines = new List<string>();
        using var log = new LogService(lines.Add, env: env);
        Assert.False(env.ConsoleLogEnabled);
        Assert.False(log.Enabled);
        log.Request(null!);
        log.Receive(null!);
        log.ClientIO("settings.json", "无效 JSON");
        log.ServerIO("role.json", "无效 JSON");
        Assert.Empty(lines);
        env.ConsoleLogEnabled = true;
        Assert.True(log.Enabled);
        log.Request(new Login());
        log.Receive(new LoginRt());
        log.ClientIO("settings.json", "{}");
        log.ServerIO("role.json", "{}");
        Assert.Equal(4, lines.Count);
        env.ConsoleLogEnabled = false;
        log.Request(null!);
        log.Receive(null!);
        log.ClientIO("settings.json", "无效 JSON");
        log.ServerIO("role.json", "无效 JSON");
        env.ConsoleLogEnabled = true;
        Assert.Equal(4, lines.Count);
        using var restarted = new EnvService(env.SettingsDirectory);
        Assert.False(restarted.ConsoleLogEnabled);
    }

    /// <summary>console-logging：关闭时真实传输和存档继续运行，开启后仅记录后续操作。</summary>
    [Fact]
    public void DisabledLoggingPreservesNetworkAndPersistence()
    {
        var root = Path.Combine(Path.GetTempPath(), "rpg-log-toggle-" + Guid.NewGuid());
        using var env = new EnvService(Path.Combine(root, "settings"));
        var lines = new List<string>();
        using var log = new LogService(lines.Add, env: env);
        using var events = new EventBusService();
        using var net = new NetService(new MockTransport(new MockServer(Path.Combine(root, "server"),
            new Rpg.Server.Data.DataServerBagRules(8, 5, []), log.ServerIO)), events, log);
        using var settings = new SettingService(env, log);
        var responses = new List<CreateRoleRt>();
        using var subscription = events.Subscribe<CreateRoleRt>(responses.Add);
        try
        {
            net.Send(new Login { Username = "demo", Password = "demo" });
            net.Send(new CreateRole { Name = "关闭日志", Gender = RoleGenderPb.Male, HeadId = 1 });
            net.Tick(0);
            Assert.True(Assert.Single(responses).Success);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "server"), "*.role.json"));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "server"), "*.bag.json"));
            settings.SetMasterVolume(0.4f);
            Assert.True(File.Exists(settings.SystemPath));
            Assert.DoesNotContain("ConsoleLogEnabled", File.ReadAllText(settings.SystemPath));
            Assert.Empty(lines);
            env.ConsoleLogEnabled = true;
            settings.SetMasterVolume(0.5f);
            Assert.StartsWith("[Client][IO]", Assert.Single(lines));
            net.Send(new CreateRole { Name = "开启日志", Gender = RoleGenderPb.Female, HeadId = 1 });
            net.Tick(0);
            Assert.Equal(5, lines.Count);
            Assert.Equal(2, responses.Count);
            Assert.True(responses[1].Success);
            env.ConsoleLogEnabled = false;
            net.Send(new CreateRole { Name = "再次关闭", Gender = RoleGenderPb.Male, HeadId = 1 });
            net.Tick(0);
            Assert.Equal(5, lines.Count);
            Assert.Equal(3, responses.Count);
            Assert.True(responses[2].Success);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>只有指定标记着色，动态内容不能覆盖颜色或注入富文本。</summary>
    [Fact]
    public void OnlySpecifiedTagsAreColored()
    {
        var lines = new List<string>();
        var rich = new List<bool>();
        using var log = new LogService(text => { lines.Add(text); rich.Add(true); },
            text => { lines.Add(text); rich.Add(false); });
        log.Request(new CreateRole { Name = "[color=red]名字[/color]" });
        log.Receive(new LoginRt { Success = true });
        log.Receive(new BagChangedNotice());
        log.ClientIO("system.json", "{\"text\":\"[color=red]内容[/color]\"}");
        log.ServerIO("1001.role.json", "{}");
        Assert.StartsWith("[Client][color=green][Request][/color]CreateRole{", lines[0]);
        Assert.Contains("\\u005Bcolor=red\\u005D名字\\u005B/color\\u005D", lines[0]);
        Assert.StartsWith("[Client][color=#ffa500][Response][/color]LoginRt{", lines[1]);
        Assert.StartsWith("[Client][color=#ffa500][Notice][/color]BagChangedNotice{", lines[2]);
        Assert.Equal("[Client][IO][system.json]{\"text\":\"[color=red]内容[/color]\"}", lines[3]);
        Assert.Equal("[Server][IO][1001.role.json]{}", lines[4]);
        Assert.Equal(new[] { true, true, true, false, false }, rich);
        Assert.All(lines.Take(3), line => Assert.Single(Regex.Matches(line, @"\[color=[^]]+\]")));
        log.Dispose();
        Assert.Throws<ObjectDisposedException>(() => log.ClientIO("x", "y"));
    }

    /// <summary>IO 去掉排版换行及缩进，但保留 JSON 字符串内部空格与转义换行。</summary>
    [Fact]
    public void IoCompactionPreservesStringWhitespace()
    {
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        log.ClientIO("system.json", "{\n  \"text\": \"保留  空格\\n换行\",\n  \"values\": [1, 2]\n}");
        Assert.Equal("[Client][IO][system.json]{\"text\":\"保留  空格\\n换行\",\"values\":[1,2]}", Assert.Single(lines));
    }

    /// <summary>实际发送冻结数据，接收在发布前记录，取消、旧代次和坏包不产生成功日志。</summary>
    [Fact]
    public void NetworkLogsActualPacketsBeforeDispatch()
    {
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        using var events = new EventBusService();
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, events, log);
        var request = new Login { Username = "原始" };
        net.Send(request);
        request.Username = "修改";
        Assert.Empty(lines);
        net.Tick(0);
        Assert.Contains("原始", Assert.Single(lines));
        Assert.DoesNotContain("修改", lines[0]);
        using var listener = events.Subscribe<LoginRt>(_ => Assert.Contains("[Response]", lines.Last()));
        transport.Inject(MessageCodec.Encode(new LoginRt(), 1, false));
        transport.Inject(MessageCodec.Encode(new RoleStateNotice(), 1, false));
        net.Tick(0);
        Assert.Equal(3, lines.Count);
        Assert.Contains("[Notice]", lines[2]);
        net.Send(new Login());
        net.ResetSession();
        transport.Inject(MessageCodec.Encode(new LoginRt(), 1, false));
        transport.Inject([255]);
        net.Tick(0);
        Assert.Equal(3, lines.Count);
    }

    /// <summary>发送失败仍保留请求尝试，且先于网络错误报告。</summary>
    [Fact]
    public void FailedSendLogsAttemptBeforeError()
    {
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        using var events = new EventBusService();
        using var transport = new DoubleNetTransport { FailSend = true };
        using var net = new NetService(transport, events, log);
        var errors = new List<Exception>();
        net.Error += error =>
        {
            Assert.Contains("[Request]", Assert.Single(lines));
            errors.Add(error);
        };
        net.Send(new Login());
        net.Tick(0);
        Assert.Contains("[Request]", Assert.Single(lines));
        Assert.Single(errors);
    }

    /// <summary>验证 console-logging 的发送前记录场景：同步服务器写盘必须排在请求之后、响应之前。</summary>
    [Fact]
    public void RequestPrecedesSynchronousServerIoAndResponse()
    {
        var root = Path.Combine(Path.GetTempPath(), "rpg-log-order-" + Guid.NewGuid());
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        using var events = new EventBusService();
        using var net = new NetService(new MockTransport(new MockServer(root,
            new Rpg.Server.Data.DataServerBagRules(8, 5, []), log.ServerIO)), events, log);
        try
        {
            net.Send(new Login { Username = "demo", Password = "demo" });
            net.Tick(0);
            lines.Clear();
            net.Send(new CreateRole { Name = "顺序测试", Gender = RoleGenderPb.Male, HeadId = 1 });
            Assert.Empty(lines);
            net.Tick(0);
            Assert.Equal(4, lines.Count);
            Assert.StartsWith("[Client][color=green][Request][/color]CreateRole{", lines[0]);
            Assert.StartsWith("[Server][IO][", lines[1]);
            Assert.Contains(".bag.json]", lines[1]);
            Assert.StartsWith("[Server][IO][", lines[2]);
            Assert.Contains(".role.json]", lines[2]);
            Assert.StartsWith("[Client][color=#ffa500][Response][/color]CreateRoleRt{", lines[3]);
            Assert.Contains("\"success\":true", lines[3]);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>系统与角色设置、服务器存档只在提交后记录实际 JSON，失败及读取不记 IO。</summary>
    [Fact]
    public void FileLogsMatchCommittedContentAndIgnoreFailedWrites()
    {
        var root = Path.Combine(Path.GetTempPath(), "rpg-log-" + Guid.NewGuid());
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        using var env = new EnvService(root);
        try
        {
            using var setting = new SettingService(env, log);
            Assert.Empty(lines);
            setting.SetMasterVolume(0.5f);
            Assert.Equal("[Client][IO][system.json]" + Compact(File.ReadAllText(setting.SystemPath)), Assert.Single(lines));
            setting.SelectRole(1);
            setting.SetHotbarVisible(2, true);
            Assert.Equal("[Client][IO][1.json]" + Compact(File.ReadAllText(setting.RolePath)), lines[1]);
            var repo = new RepoServerRole(Path.Combine(root, "server"), log.ServerIO);
            var role = repo.Create("测试", Rpg.Server.Data.ServerRoleGender.Male, 1);
            Assert.Equal("[Server][IO][" + role.RoleId + ".bag.json]" +
                Compact(File.ReadAllText(repo.GetBagPath(role.RoleId))), lines[2]);
            Assert.Equal("[Server][IO][" + role.RoleId + ".role.json]" +
                Compact(File.ReadAllText(repo.GetRolePath(role.RoleId))), lines[3]);
            Assert.All(lines, line => { Assert.DoesNotContain("\n", line); Assert.DoesNotContain("\r", line); });
            repo.Load(role.RoleId);
            Assert.Equal(4, lines.Count);
            using (var locked = new FileStream(repo.GetRolePath(role.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => repo.SaveRole(role with { X = role.X + 1 }));
            Directory.CreateDirectory(setting.SystemPath + ".tmp");
            Assert.Throws<UnauthorizedAccessException>(() => setting.SetMasterVolume(0.4f));
            Assert.Equal(4, lines.Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>对比写盘 JSON 的等价紧凑文本，保留字符串内容。</summary>
    private static string Compact(string content)
    {
        using var json = JsonDocument.Parse(content);
        return JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>控制台输出失败不能把成功收发与保存变成业务失败。</summary>
    [Fact]
    public void OutputFailuresDoNotBreakCommittedOperations()
    {
        using var log = new LogService(_ => throw new IOException("输出失败"));
        using var events = new EventBusService();
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, events, log);
        var errors = new List<Exception>();
        net.Error += errors.Add;
        net.Send(new Login());
        net.Tick(0);
        Assert.Single(transport.Sent);
        Assert.Empty(errors);
        var root = Path.Combine(Path.GetTempPath(), "rpg-log-failure-" + Guid.NewGuid());
        using var env = new EnvService(root);
        try
        {
            using var setting = new SettingService(env, log);
            setting.SetMasterVolume(0.2f);
            Assert.True(File.Exists(setting.SystemPath));
            var repo = new RepoServerRole(Path.Combine(root, "server"), log.ServerIO);
            var role = repo.Create("日志失败", Rpg.Server.Data.ServerRoleGender.Male, 1);
            Assert.Equal(role, repo.Load(role.RoleId));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
