using System.Text.Json.Nodes;
using Godot;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Net;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Session.Data;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/role-management 的创建、删除、失败、重启及外观贯通。</summary>
[Trait("Runtime", "Godot")]
public sealed class RoleManagementTests : RegisteredServiceTestScope
{
    /// <summary>客户端与服务端分别拒绝未认证及已进入角色的管理请求。</summary>
    [Fact]
    public void ManagementRequiresAuthenticatedRoleList()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        CreateRoleRt? created = null;
        DeleteRoleRt? deleted = null;
        using var create = EventBus.Subscribe<CreateRoleRt>(response => created = response);
        using var delete = EventBus.Subscribe<DeleteRoleRt>(response => deleted = response);
        fixture.Net.Send(new CreateRole { Name = "越权角色", Gender = RoleGenderPb.Male, HeadId = 1 });
        fixture.Net.Send(new DeleteRole { RoleId = 1001 });
        fixture.Net.Tick(0);
        Assert.False(created!.Success);
        Assert.False(deleted!.Success);
        fixture.Login();
        fixture.SelectRole(1001);
        Assert.Throws<InvalidOperationException>(() => fixture.Session!.CreateRole("越权角色", RoleGender.Male, 1));
        fixture.Net.Send(new CreateRole { Name = "越权角色", Gender = RoleGenderPb.Male, HeadId = 1 });
        fixture.Net.Send(new DeleteRole { RoleId = 1001 });
        fixture.Net.Tick(0);
        Assert.False(created.Success);
        Assert.False(deleted.Success);
        Assert.Equal(2, new RepoServerRole(fixture.DirectoryPath).List().Count);
    }

    /// <summary>创建目标被同名目录占用时报告失败，不留下摘要或部分文件，释放后可重试。</summary>
    [Fact]
    public void FailedCreateCanRetryWithoutPartialRole()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var blocked = Path.Combine(fixture.DirectoryPath, "1003.json");
        Directory.CreateDirectory(blocked);
        var before = fixture.Session!.Roles;
        CreateRoleRt? result = null;
        using var listener = EventBus.Subscribe<CreateRoleRt>(response => result = response);
        fixture.Session.CreateRole("重试创建", RoleGender.Male, 2);
        fixture.Net.Tick(0);
        Assert.False(result!.Success);
        Assert.Same(before, fixture.Session.Roles);
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        Directory.Delete(blocked);
        fixture.Session.CreateRole("重试创建", RoleGender.Male, 2);
        fixture.Net.Tick(0);
        Assert.True(result.Success, result.Error);
        Assert.Equal(3, fixture.Session.Roles.Count);
    }

    /// <summary>管理响应包含非法摘要时保留完整旧列表，并阻止普通监听消费部分更新。</summary>
    [Fact]
    public void MalformedManagementResponseDoesNotPublishPartialList()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var before = fixture.Session!.Roles;
        var delivered = false;
        using var listener = EventBus.Subscribe<CreateRoleRt>(_ => delivered = true);
        EventBus.Publish(new CreateRoleRt { Success = true, Roles =
        {
            new RoleSummaryPb { RoleId = 5, Name = "合法", Level = 1, Gender = RoleGenderPb.Male },
            new RoleSummaryPb { RoleId = 6, Name = "非法头型", Level = 1, Gender = RoleGenderPb.Male, HeadId = 999 }
        }});
        Assert.False(delivered);
        Assert.Same(before, fixture.Session.Roles);
        Assert.Single(fixture.Errors);
    }

    /// <summary>非默认头型在真实协议、缓存、ECS、移动落盘与重启之间保持一致。</summary>
    [Theory]
    [InlineData(RoleGender.Male, 74)]
    [InlineData(RoleGender.Female, 37)]
    public void CreateHeadFlowsThroughWorldAndRestart(RoleGender gender, int headId)
    {
        using var fixture = new NetworkTestFixture(EventBus);
        Assert.Empty(fixture.Login().Roles);
        Assert.Empty(fixture.Login().Roles);
        var session = fixture.Session!;
        var observed = false;
        using var listener = EventBus.Subscribe<CreateRoleRt>(response =>
        {
            Assert.True(response.Success, response.Error);
            Assert.Single(session.Roles);
            observed = true;
        });
        session.CreateRole("  新角色  ", gender, headId);
        Assert.Empty(session.Roles);
        fixture.Net.Tick(0);
        Assert.True(observed);
        var summary = session.Roles.Single(role => role.Name == "新角色");
        Assert.Equal(headId, summary.HeadId);
        fixture.SelectRole(summary.RoleId);
        Assert.Equal(headId, session.Role.HeadId);
        var world = new WorldManager();
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(new Rpg.Config.Data.MapCfg())));
        world.Enqueue(new InitializeRoleAction(session.Role));
        world.Tick(0);
        Assert.Equal(headId, world.ActorState!.HeadId);
        Assert.Equal(gender, world.ActorState.Gender);
        session.SubmitMove(1, session.Role.Position + Vector2.One);
        fixture.Net.Tick(0.5);
        Assert.Equal(headId, session.Role.HeadId);
        using var restarted = new NetworkTestFixture(EventBus, fixture.DirectoryPath);
        Assert.Equal(headId, restarted.Login().Roles.Single(role => role.RoleId == summary.RoleId).HeadId);
        Assert.Equal(headId, restarted.SelectRole(summary.RoleId).Role.HeadId);
    }

    /// <summary>删空后重建服务器不会恢复演示角色，旧 ID 不再可选且创建不复用编号。</summary>
    [Fact]
    public void DeleteAllSurvivesRestartAndAllowsCreate()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        foreach (var role in fixture.Session!.Roles.ToArray())
        {
            fixture.Session.DeleteRole(role.RoleId);
            fixture.Net.Tick(0);
        }
        Assert.Empty(fixture.Session.Roles);
        using var restarted = new NetworkTestFixture(EventBus, fixture.DirectoryPath);
        Assert.Empty(restarted.Login().Roles);
        SelectRoleRt? selection = null;
        using var listener = EventBus.Subscribe<SelectRoleRt>(response => selection = response);
        restarted.Session!.SelectRole(1001);
        restarted.Net.Tick(0);
        Assert.False(selection!.Success);
        restarted.Session.LeaveRole();
        restarted.Session.CreateRole("重新创建", RoleGender.Female, 74);
        restarted.Net.Tick(0);
        Assert.True(restarted.Session.Roles.Single().RoleId > 1002);
    }

    /// <summary>非法名字、重复名字或外观不改变列表，失败后合法请求可重试。</summary>
    [Theory]
    [InlineData("", 1)]
    [InlineData("12345678901234567", 1)]
    [InlineData("坏\n名字", 1)]
    [InlineData("晨星", 1)]
    [InlineData("新角色", 0)]
    [InlineData("新角色", 75)]
    public void InvalidCreatePreservesList(string name, int headId)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var before = fixture.Session!.Roles;
        CreateRoleRt? result = null;
        using var listener = EventBus.Subscribe<CreateRoleRt>(response => result = response);
        fixture.Session.CreateRole(name, RoleGender.Male, headId);
        fixture.Net.Tick(0);
        Assert.False(result!.Success);
        Assert.NotEmpty(result.Error);
        Assert.Same(before, fixture.Session.Roles);
        fixture.Session.CreateRole("合法重试", RoleGender.Male, 1);
        fixture.Net.Tick(0);
        Assert.True(result.Success, result.Error);
    }

    /// <summary>文件锁导致删除失败时，旧文件与缓存列表保持原值。</summary>
    [Fact]
    public void FailedDeletePreservesFileAndCache()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var path = new RepoServerRole(fixture.DirectoryPath).GetRolePath(1001);
        var original = File.ReadAllText(path);
        var before = fixture.Session!.Roles;
        DeleteRoleRt? result = null;
        using var listener = EventBus.Subscribe<DeleteRoleRt>(response => result = response);
        using (var locked = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
        {
            fixture.Session.DeleteRole(1001);
            fixture.Net.Tick(0);
            Assert.False(result!.Success);
            Assert.Same(before, fixture.Session.Roles);
            Assert.Equal(original, File.ReadAllText(path));
        }
        fixture.Session.DeleteRole(1001);
        fixture.Net.Tick(0);
        Assert.True(result!.Success, result.Error);
        Assert.Single(fixture.Session.Roles);
    }

    /// <summary>旧 JSON 缺失头型字段时默认 1，旧协议编号 0 可往返且非法编号被拒绝。</summary>
    [Fact]
    public void LegacyHeadAndNewProtocolAreCompatible()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var path = new RepoServerRole(fixture.DirectoryPath).GetRolePath(1001);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json.Remove("HeadId");
        File.WriteAllText(path, json.ToJsonString());
        Assert.Equal(1, fixture.SelectRole(1001).Role.HeadId);
        Assert.Equal("role.proto", SelectRole.Descriptor.File.Name);
        Assert.Equal("role.proto", RoleSummaryPb.Descriptor.File.Name);
        Assert.Equal("role.proto", RoleWorldStatePb.Descriptor.File.Name);
        var message = new RoleSummaryPb { RoleId = 1, Name = "旧角色", Level = 1, Gender = RoleGenderPb.Male };
        Assert.Equal(message, RoleSummaryPbData.FromPb(message).ToPb());
        message.HeadId = 74;
        Assert.Equal(message, RoleSummaryPbData.FromPb(message).ToPb());
        message.HeadId = 75;
        Assert.Throws<InvalidDataException>(() => RoleSummaryPbData.FromPb(message));
    }
}
