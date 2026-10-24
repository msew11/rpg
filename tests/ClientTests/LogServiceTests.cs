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

    /// <summary>底层发送失败只报告网络错误，不输出成功请求日志。</summary>
    [Fact]
    public void FailedSendDoesNotLogSuccessfulRequest()
    {
        var lines = new List<string>();
        using var log = new LogService(lines.Add);
        using var events = new EventBusService();
        using var transport = new DoubleNetTransport { FailSend = true };
        using var net = new NetService(transport, events, log);
        var errors = new List<Exception>();
        net.Error += errors.Add;
        net.Send(new Login());
        net.Tick(0);
        Assert.Empty(lines);
        Assert.Single(errors);
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
