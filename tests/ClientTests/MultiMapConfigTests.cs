using System.Text.Json.Nodes;
using Rpg.Config;
using Rpg.Services;
using Rpg.Tests;

namespace ClientTests;

/// <summary>验证 client-config-map 的数值多例地图、初始地图引用及 World 选图生命周期。</summary>
[Trait("Runtime", "Godot")]
public sealed class MultiMapConfigTests : IDisposable
{
    /// <summary>测试独占配置及存储根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-multi-map-" + Guid.NewGuid());

    /// <summary>准备 ID 为 1 和 2 的地图，2 排在前且设为初始地图。</summary>
    public MultiMapConfigTests()
    {
        Directory.CreateDirectory(_directory);
        foreach (var name in new[] { "map", "item", "basic", "equipment" })
            File.Copy(Path.Combine(ConfigTestFixture.FindRoot(), "config", name + ".json"), Path.Combine(_directory, name + ".json"));
        var maps = JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, "map.json")))!.AsArray();
        var second = maps[0]!.DeepClone(); second["id"] = 2; second["width"] = 200; second["height"] = 300;
        second["overrides"] = new JsonArray(); second.AsObject().Remove("bgmTrackId"); maps.Insert(0, second);
        File.WriteAllText(Path.Combine(_directory, "map.json"), maps.ToJsonString());
        var basic = JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, "basic.json")))!;
        basic["initialMapId"] = 2;
        File.WriteAllText(Path.Combine(_directory, "basic.json"), basic.ToJsonString());
    }

    /// <summary>使用真实 Godot 配置加载、独立服务器和完整九服务组装。</summary>
    private EnvService Env() => new(Path.Combine(_directory, "settings"), Path.Combine(_directory, "server"),
        configDirectory: _directory, schemaDirectory: Path.Combine(ConfigTestFixture.FindRoot(), "schemas"));

    /// <summary>初始化不选图，创建角色使用配置地图，进入后 World 与 Session 同图，离开清空。</summary>
    [Fact]
    public void WorldSelectsRoleMapRatherThanFirstOrHardcodedMap()
    {
        using var services = GameServicesTestFactory.Register(Env());
        Assert.False(GameServices.World.HasWorld);
        var session = GameServices.Session;
        session.Login("demo", "demo"); GameServices.Net.Tick(0);
        session.CreateRole("第二张地图", Rpg.Const.RoleGender.Male, 1); GameServices.Net.Tick(0);
        session.SelectRole(Assert.Single(session.Roles).RoleId); GameServices.Net.Tick(0);
        Assert.Equal(2, session.Role.MapId); Assert.Equal(2, session.Map!.MapId);
        GameServices.World.InitializeRole();
        Assert.Equal(200, GameServices.World.MapConfig.Width);
        // client-config-map：未配置曲目的地图仍可进入，并保持无 BGM 编号。
        Assert.Equal("", GameServices.World.MapConfig.BgmTrackId);
        Assert.Equal(session.Map.Sequence, GameServices.World.MapObjects!.Sequence);
        GameServices.World.LeaveRole(); session.LeaveRole();
        Assert.Null(GameServices.World.MapObjects); Assert.Null(session.Map);
        Assert.Throws<InvalidOperationException>(() => GameServices.World.MapConfig);
    }

    /// <summary>重复地图身份和不存在的初始地图都在启动阶段诊断。</summary>
    [Fact]
    public void DuplicateAndMissingInitialMapAreRejected()
    {
        var path = Path.Combine(_directory, "basic.json");
        var basic = JsonNode.Parse(File.ReadAllText(path))!; basic["initialMapId"] = 999;
        File.WriteAllText(path, basic.ToJsonString());
        Assert.Contains("initialMapId", Assert.Throws<InvalidDataException>(() => new ConfigService(Env())).Message);
        basic["initialMapId"] = 2; File.WriteAllText(path, basic.ToJsonString());
        var mapsPath = Path.Combine(_directory, "map.json");
        var maps = JsonNode.Parse(File.ReadAllText(mapsPath))!.AsArray(); maps[1]!["id"] = 2;
        File.WriteAllText(mapsPath, maps.ToJsonString());
        Assert.Contains("重复", Assert.Throws<InvalidDataException>(() => new ConfigService(Env())).Message);
    }

    /// <summary>client-config-map：非法曲目编号在地图配置加载时拒绝。</summary>
    [Fact]
    public void InvalidBgmTrackIdIsRejected()
    {
        var mapsPath = Path.Combine(_directory, "map.json");
        var maps = JsonNode.Parse(File.ReadAllText(mapsPath))!.AsArray();
        maps[1]!["bgmTrackId"] = "../M001";
        File.WriteAllText(mapsPath, maps.ToJsonString());
        var error = Assert.Throws<InvalidDataException>(() => new ConfigService(Env()));
        Assert.Contains("map.json", error.Message);
        Assert.Contains("Schema", error.Message);
    }

    /// <summary>移除测试配置与存档。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
