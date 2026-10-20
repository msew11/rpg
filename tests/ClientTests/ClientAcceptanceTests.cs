using Godot;
using Rpg.Config;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-config-map 与 client-session 的配置及会话场景。</summary>
public sealed class ClientAcceptanceTests
{
    /// <summary>合法地图配置能组合为完整地图，未知配置会给出文件诊断。</summary>
    [Fact]
    public void ConfigLoadsAllFilesAndRejectsUnknownType()
    {
        var root = FindRoot();
        var repo = new RepoGameConfig();
        var config = repo.Load(Path.Combine(root, "config"), Path.Combine(root, "schemas"));
        Assert.Equal(1000, config.Map.Width);
        Assert.Equal(1000, config.Map.Height);
        Assert.Equal(32, config.Map.TileSize);
        var map = new MapManager(config.Map);
        Assert.Equal("accent", map.TileAt(505, 500));
        Assert.Equal("ground", map.TileAt(1, 1));

        var temporary = Path.Combine(Path.GetTempPath(), "rpg-config-test-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        try
        {
            File.WriteAllText(Path.Combine(temporary, "unexpected.json"), "{\"kind\":\"unknown\"}");
            var error = Assert.Throws<InvalidDataException>(() => repo.Load(temporary, Path.Combine(root, "schemas")));
            Assert.Contains("unexpected.json", error.Message);
            File.Delete(Path.Combine(temporary, "unexpected.json"));
            File.WriteAllText(Path.Combine(temporary, "invalid.json"),
                "{\"kind\":\"map\",\"id\":\"bad\",\"width\":0,\"height\":1000,\"tileSize\":32,\"defaultTile\":\"ground\",\"overrides\":[]}");
            error = Assert.Throws<InvalidDataException>(() => repo.Load(temporary, Path.Combine(root, "schemas")));
            Assert.Contains("invalid.json", error.Message);
            Assert.Contains("Schema", error.Message);
        }
        finally { Directory.Delete(temporary, true); }
    }

    /// <summary>地图管理类只返回视口附近的格子范围。</summary>
    [Fact]
    public void MapVisibleRegionDoesNotExpandWholeWorld()
    {
        var map = new MapManager(new DataMapConfig
        {
            Kind = "map", Id = "test", Width = 1000, Height = 1000, TileSize = 32,
            DefaultTile = "ground"
        });
        var region = map.VisibleTiles(new Rect2(16000, 16000, 1280, 720));
        Assert.True((region.Right - region.Left + 1) * (region.Bottom - region.Top + 1) < 1500);
    }

    /// <summary>固定账号成功、错误凭据失败，切换角色得到隔离缓存。</summary>
    [Fact]
    public void LoginAndRoleSelectionCreateIsolatedCache()
    {
        var source = new MockStateSource();
        Assert.False(source.Login("demo", "wrong").Success);
        var result = source.Login("demo", "demo");
        Assert.True(result.Success);
        Assert.True(result.Roles.Count >= 2);
        Assert.NotEqual(result.Roles[0].RoleId, result.Roles[1].RoleId);

        using var first = source.SelectRole(result.Roles[0].RoleId);
        var firstCache = first.Cache;
        first.Dispose();
        using var second = source.SelectRole(result.Roles[1].RoleId);
        Assert.NotSame(firstCache, second.Cache);
        Assert.Equal(result.Roles[1].RoleId, second.Cache.Role.Role.RoleId);
    }

    /// <summary>状态源延迟确认先更新缓存，旧版本不会再次通知。</summary>
    [Fact]
    public void DelayedAuthorityUpdateLandsInCache()
    {
        var source = new MockStateSource();
        using var session = source.SelectRole(1001);
        var notifications = 0;
        session.Cache.RoleChanged += _ => notifications++;
        session.SubmitMove(1, session.Role.Position + new Vector2(100, 0));
        session.Advance(0.2);
        Assert.Equal(0, notifications);
        session.Advance(0.3);
        Assert.Equal(1, notifications);
        Assert.Equal(1, session.Cache.Role.AcknowledgedAction);
        Assert.False(session.Cache.Apply(session.Cache.Role with { Revision = 0 }));
        Assert.Equal(1, notifications);
    }

    /// <summary>从测试输出目录向上寻找项目根目录。</summary>
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
    }
}

/// <summary>验证 openspec/specs/client-ecs-world 的 Action、重定目标与校正场景。</summary>
public sealed class WorldAcceptanceTests
{
    /// <summary>验证 client-ecs-world 的等轴坐标往返及 2:1 地块几何。</summary>
    [Fact]
    public void IsometricProjectionRoundTripsLogicalCoordinates()
    {
        var logical = new Vector2(16016, 15984);
        var screen = IsometricProjection.ToScreen(logical);
        Assert.Equal(new Vector2(16, 8000), screen);
        Assert.Equal(logical, IsometricProjection.ToLogical(screen));
        var origin = IsometricProjection.ToScreen(Vector2.Zero);
        var east = IsometricProjection.ToScreen(new Vector2(32, 0));
        var south = IsometricProjection.ToScreen(new Vector2(0, 32));
        Assert.Equal(32, east.X - south.X);
        Assert.Equal(16, east.Y + south.Y - 2 * origin.Y);
    }

    /// <summary>世界必须先于角色建立，连续点击改目标，权威状态只能经 Action 生效。</summary>
    [Fact]
    public void WorldActionOrderMovementAndCorrection()
    {
        var map = new MapManager(new DataMapConfig
        {
            Kind = "map", Id = "test", Width = 1000, Height = 1000, TileSize = 32,
            DefaultTile = "ground"
        });
        var role = new DataRole(1, "测试", 1, 10, 10, 10, 10, 0, 10, new Vector2(100, 100));
        var world = new WorldManager();
        world.Enqueue(new InitializeRoleAction(role));
        Assert.Throws<InvalidOperationException>(() => world.Tick(0));

        world.Enqueue(new InitializeWorldAction(map));
        world.Enqueue(new InitializeRoleAction(role));
        world.Tick(0);
        Assert.True(world.HasWorld);
        Assert.True(world.HasRole);
        world.Enqueue(new MoveAction(1, new Vector2(300, 100)));
        world.Tick(0.1);
        Assert.True(world.RolePosition.X > 100);
        world.Enqueue(new MoveAction(2, new Vector2(100, 300)));
        world.Tick(0);
        Assert.Equal(new Vector2(100, 300), world.RoleTarget);

        var corrected = new Vector2(120, 110);
        world.Enqueue(new SyncRoleAction(new DataRoleState(role, corrected, 1, 1)));
        Assert.NotEqual(corrected, world.RolePosition);
        world.Tick(0);
        Assert.Equal(corrected, world.RolePosition);
        Assert.Equal(new Vector2(100, 300), world.RoleTarget);
        world.Enqueue(new SyncRoleAction(new DataRoleState(role, new Vector2(999, 999), 2, 1)));
        world.Tick(0);
        Assert.Equal(corrected, world.RolePosition);
    }
}

/// <summary>验证 openspec/specs/client-settings 的系统持久化和角色隔离场景。</summary>
public sealed class SettingsAcceptanceTests
{
    /// <summary>系统设置重启可读取，角色 HUD 显隐与布局互不影响。</summary>
    [Fact]
    public void SystemAndRoleSettingsPersistSeparately()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-settings-test-" + Guid.NewGuid());
        try
        {
            var repo = new RepoSettings(directory);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "system.json")), repo.SystemPath);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "roles", "1001.json")), repo.GetRolePath(1001));
            var system = repo.LoadSystem();
            system.Width = 1600;
            system.Height = 900;
            system.MasterVolume = 0.4f;
            repo.SaveSystem(system);
            var first = repo.LoadRole(1001);
            first.VisibleHotbars[1] = true;
            first.HudPositions["portrait"] = new DataHudPosition(0.4f, 0.2f);
            repo.SaveRole(1001, first);

            var reopened = new RepoSettings(directory);
            Assert.Equal(1600, reopened.LoadSystem().Width);
            Assert.Equal(0.4f, reopened.LoadSystem().MasterVolume);
            Assert.True(reopened.LoadRole(1001).VisibleHotbars[1]);
            Assert.Equal(0.4f, reopened.LoadRole(1001).HudPositions["portrait"].X);
            Assert.False(reopened.LoadRole(1002).VisibleHotbars[1]);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>验证 openspec/specs/client-settings 的角色隔离及 HUD 拖动保存时机。</summary>
    [Fact]
    public void CurrentSettingsOwnUpdatesAndDeferredHudSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-current-settings-test-" + Guid.NewGuid());
        try
        {
            var repo = new RepoSettings(directory);
            repo.SetResolution(1600, 900);
            repo.SetMasterVolume(0.4f);
            repo.SelectRole(1001);
            Assert.False(File.Exists(repo.GetRolePath(1001)));
            Assert.Throws<ArgumentException>(() => repo.SetHotbarVisible(1, false));
            repo.SetHotbarVisible(2, true);
            repo.SetHudPosition("portrait", 0.4f, 0.2f);
            Assert.NotEqual(0.4f, new RepoSettings(directory).LoadRole(1001).HudPositions["portrait"].X);
            Assert.Equal(0.4f, repo.GetHudPosition("portrait").X);
            repo.SaveCurrentRole();
            repo.SelectRole(1002);
            Assert.False(repo.IsHotbarVisible(2));
            repo.SetHudPosition("portrait", 0.6f, 0.3f);
            repo.SelectRole(1001);
            Assert.True(repo.IsHotbarVisible(2));
            Assert.Equal(0.4f, repo.GetHudPosition("portrait").X);
            repo.ResetHudPositions();

            var reopened = new RepoSettings(directory);
            Assert.Equal(1600, reopened.Width);
            Assert.Equal(0.4f, reopened.MasterVolume);
            Assert.Equal(0.6f, reopened.LoadRole(1002).HudPositions["portrait"].X);
            Assert.Equal(DataRoleSetting.DefaultPositions()["portrait"].X,
                reopened.LoadRole(1001).HudPositions["portrait"].X);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
