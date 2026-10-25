using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Const;
using Godot;
using Rpg.Config;
using Rpg.Config.Data;
using Rpg.Net.Message;
using Rpg.Session;
using Rpg.Settings;
using Rpg.Settings.Data;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-config-map 与 client-session 的配置及会话场景。</summary>
[Trait("Runtime", "Godot")]
public sealed class ClientAcceptanceTests : RegisteredServiceTestScope
{
    /// <summary>合法地图配置能组合为完整地图，未注册文件会给出文件诊断。</summary>
    [Fact]
    [Trait("Runtime", "Godot")]
    public void ConfigLoadsAllFilesAndRejectsUnknownFile()
    {
        var root = ConfigTestFixture.FindRoot();
        var repo = new ConfigService(new EnvService("", configDirectory: Path.Combine(root, "config"), schemaDirectory: Path.Combine(root, "schemas")));
        Assert.Equal(1000, repo.Map.Items[1].Width);
        Assert.Equal(1000, repo.Map.Items[1].Height);
        Assert.Equal(32, repo.Map.Items[1].TileSize);
        Assert.Equal(1, repo.Map.Items[1].Id);
        // client-config-map：地图 1 从配置指定背景音乐。
        Assert.Equal("M015a", repo.Map.Items[1].BgmTrackId);
        var map = MapUtils.CreateMap(repo.Map.Items[1]);
        Assert.Equal("accent", MapUtils.TileAt(map, 505, 500));
        Assert.Equal("ground", MapUtils.TileAt(map, 1, 1));

        var temporary = Path.Combine(Path.GetTempPath(), "rpg-config-test-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        File.Copy(Path.Combine(root, "config", "basic.json"), Path.Combine(temporary, "basic.json"));
        File.WriteAllText(Path.Combine(temporary, "item.json"), "[]");
        try
        {
            File.WriteAllText(Path.Combine(temporary, "unexpected.json"), "{}");
            var error = Assert.Throws<InvalidDataException>(() => new ConfigService(new EnvService("", configDirectory: temporary, schemaDirectory: Path.Combine(root, "schemas"))));
            Assert.Contains("unexpected.json", error.Message);
            Assert.Equal(1, repo.Map.Items[1].Id);
            File.Delete(Path.Combine(temporary, "unexpected.json"));
            File.WriteAllText(Path.Combine(temporary, "map.json"),
                "{\"id\":\"bad\",\"width\":0,\"height\":1000,\"tileSize\":32,\"defaultTile\":\"ground\",\"overrides\":[]}");
            error = Assert.Throws<InvalidDataException>(() => new ConfigService(new EnvService("", configDirectory: temporary, schemaDirectory: Path.Combine(root, "schemas"))));
            Assert.Contains("map.json", error.Message);
            Assert.Contains("Schema", error.Message);
            File.Delete(Path.Combine(temporary, "map.json"));
            File.WriteAllText(Path.Combine(temporary, "wrong-name.json"),
                File.ReadAllText(Path.Combine(root, "config", "map.json")));
            error = Assert.Throws<InvalidDataException>(() => new ConfigService(new EnvService("", configDirectory: temporary, schemaDirectory: Path.Combine(root, "schemas"))));
            Assert.Contains("wrong-name.json", error.Message);
            Assert.Contains("map.json", error.Message);
            Assert.Equal(1, repo.Map.Items[1].Id);
            File.Delete(Path.Combine(temporary, "wrong-name.json"));
            var duplicateDirectory = Path.Combine(temporary, "second");
            Directory.CreateDirectory(duplicateDirectory);
            var validMap = File.ReadAllText(Path.Combine(root, "config", "map.json"));
            File.WriteAllText(Path.Combine(temporary, "map.json"), validMap);
            File.WriteAllText(Path.Combine(duplicateDirectory, "map.json"), validMap);
            error = Assert.Throws<InvalidDataException>(() => new ConfigService(new EnvService("", configDirectory: temporary, schemaDirectory: Path.Combine(root, "schemas"))));
            Assert.Contains("配置目录不允许子目录", error.Message);
            Assert.Equal(1, repo.Map.Items[1].Id);
        }
        finally { Directory.Delete(temporary, true); }
    }

    /// <summary>地图工具只返回视口附近的格子范围。</summary>
    [Fact]
    public void MapVisibleRegionDoesNotExpandWholeWorld()
    {
        var map = MapUtils.CreateMap(new MapCfg { Width = 1000, Height = 1000, TileSize = 32 });
        var region = MapUtils.VisibleTiles(map, new Rect2(16000, 16000, 1280, 720));
        Assert.True((region.Right - region.Left + 1) * (region.Bottom - region.Top + 1) < 1500);
    }

    /// <summary>固定账号成功、错误凭据不建会话，切换角色复用账号会话并隔离角色数据。</summary>
    [Fact]
    public void LoginAndRoleSelectionCreateIsolatedCache()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        Assert.Null(source.Session);
        Assert.False(source.Login("demo", "wrong").Success);
        Assert.Null(source.Session);
        var result = source.Login("demo", "demo");
        Assert.True(result.Success);
        Assert.True(result.Roles.Count >= 2);
        Assert.NotEqual(result.Roles[0].RoleId, result.Roles[1].RoleId);

        var account = source.Session!;
        Assert.True(account.LoginCache.IsLoggedIn);
        Assert.False(account.RoleCache.IsInitialized);
        Assert.Equal(0, account.RoleId);
        Assert.Throws<InvalidOperationException>(() => account.SubmitMove(1, Vector2.Zero));
        using var first = source.SelectRole(result.Roles[0].RoleId);
        var firstState = first.RoleCache.State;
        first.LeaveRole();
        Assert.False(account.RoleCache.IsInitialized);
        Assert.Equal(0, account.RoleId);
        using var second = source.SelectRole(result.Roles[1].RoleId);
        Assert.Same(account, second);
        Assert.Same(first.RoleCache, second.RoleCache);
        Assert.NotSame(firstState, second.RoleCache.State);
        Assert.Same(result.Roles, second.LoginCache.Roles);
        Assert.Equal(result.Roles[1].RoleId, second.RoleCache.State.RoleId);
    }

    /// <summary>网络延迟确认先更新缓存，旧版本不能回滚权威状态。</summary>
    [Fact]
    public void DelayedAuthorityUpdateLandsInCache()
    {
        using var source = new NetworkTestFixture(EventBus, seedRoles: true);
        source.Login();
        using var session = source.SelectRole(1001);
        var notifications = 0;
        using var subscription = EventBus.Subscribe<MoveRt>(_ => notifications++);
        session.SubmitMove(1, session.Role.Position + new Vector2(100, 0));
        source.Net.Tick(0.2);
        Assert.Equal(0, notifications);
        source.Net.Tick(0.3);
        Assert.Equal(1, notifications);
        Assert.Equal(1, session.RoleCache.State.AcknowledgedAction);
        var current = session.RoleCache.State;
        EventBus.Publish(new RoleStateNotice { State = current.ToPb() });
        Assert.Same(current, session.RoleCache.State);
        Assert.Equal(1, notifications);
    }

    /// <summary>client-ecs-world：已有模拟服务端在移动过程中采用更新后的速度。</summary>
    [Fact]
    public void ExistingMockServerUsesUpdatedMovementSpeed()
    {
        var speed = 180;
        using var source = new NetworkTestFixture(EventBus, seedRoles: true, movementSpeed: () => speed);
        source.Login();
        using var session = source.SelectRole(1001);
        var start = session.Role.Position;
        session.SubmitMove(1, start + new Vector2(1000, 0));
        source.Net.Tick(0.2);
        speed = 300;
        source.Net.Tick(0.3);
        Assert.InRange(session.Role.Position.X - start.X, 128, 129); // 36 + 90 + 横向 3 单位修正。
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
        var map = MapUtils.CreateMap(new MapCfg { Width = 1000, Height = 1000, TileSize = 32 });
        var role = new RoleWorldStatePbData(1, "测试", 1, 10, 10, 10, 10, 0, 10, new Vector2(100, 100), RoleGender.Male, 0, 0);
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
        world.Enqueue(new SyncRoleAction(role with { Position = corrected, Revision = 1, PositionRevision = 1, AcknowledgedAction = 1 }));
        Assert.NotEqual(corrected, world.RolePosition);
        world.Tick(0);
        Assert.Equal(corrected, world.RolePosition);
        Assert.Equal(new Vector2(100, 300), world.RoleTarget);
        world.Enqueue(new SyncRoleAction(role with { Position = new Vector2(999, 999), Revision = 2, PositionRevision = 2, AcknowledgedAction = 1 }));
        world.Tick(0);
        Assert.Equal(new Vector2(999, 999), world.RolePosition);
    }

    /// <summary>client-ecs-world：GM 修改速度后，已有世界的下一帧采用新速度。</summary>
    [Fact]
    public void ExistingWorldUsesUpdatedMovementSpeed()
    {
        var speed = 180;
        var world = new WorldManager(() => speed);
        var role = new RoleWorldStatePbData(1, "测试", 1, 10, 10, 10, 10, 0, 10,
            new Vector2(100, 100), RoleGender.Male, 0, 0);
        world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(new MapCfg { Width = 1000, Height = 1000, TileSize = 32 })));
        world.Enqueue(new InitializeRoleAction(role));
        world.Enqueue(new MoveAction(1, new Vector2(500, 100)));
        world.Tick(1);
        Assert.Equal(280, world.RolePosition.X);
        speed = 60;
        world.Tick(1);
        Assert.Equal(340, world.RolePosition.X);
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
            using var setting = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "system.json")), setting.SystemPath);
            setting.SetResolution(1600, 900);
            setting.SetVolumes(0.4f, 0.6f);
            setting.SelectRole(1001);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "roles", "1001.json")), setting.RolePath);
            setting.SetHotbarVisible(2, true);
            setting.SetHudPosition("portrait", 0.4f, 0.2f);
            setting.SaveCurrentRole();

            using var reopened = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(1600, reopened.Width);
            Assert.Equal(0.4f, reopened.MasterVolume);
            Assert.Equal(0.6f, reopened.EffectsVolume);
            reopened.SelectRole(1001);
            Assert.True(reopened.IsHotbarVisible(2));
            Assert.Equal(0.4f, reopened.GetHudPosition("portrait").X);
            reopened.SelectRole(1002);
            Assert.False(reopened.IsHotbarVisible(2));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    /// <summary>验收 client-settings：旧系统文件和非法模式回退，三种模式均可保存。</summary>
    [Fact]
    public void DisplayModePersistsAndLegacySettingsRemainWindowed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-display-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(directory);
            using var setting = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            File.WriteAllText(setting.SystemPath, "{\"Width\":1920,\"Height\":1080,\"MasterVolume\":0.5}");
            using var legacy = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(WindowDisplayMode.Windowed, legacy.WindowMode);
            Assert.Equal(1920, legacy.Width);
            Assert.Equal(0.8f, legacy.EffectsVolume);
            legacy.SetDisplay(1920, 1080, WindowDisplayMode.Fullscreen);
            using var fullscreen = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(WindowDisplayMode.Fullscreen, fullscreen.WindowMode);
            fullscreen.SetDisplay(1280, 720, WindowDisplayMode.BorderlessFullscreen);
            using var borderless = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(WindowDisplayMode.BorderlessFullscreen, borderless.WindowMode);
            borderless.SetDisplay(1024, 768, WindowDisplayMode.Windowed);
            using var windowed = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(WindowDisplayMode.Windowed, windowed.WindowMode);
            File.WriteAllText(setting.SystemPath, "{\"Width\":1920,\"Height\":1080,\"WindowMode\":99}");
            using var invalid = new SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(WindowDisplayMode.Windowed, invalid.WindowMode);
            Assert.Equal(1920, invalid.Width);
            Directory.CreateDirectory(invalid.SystemPath + ".tmp");
            Assert.NotNull(Record.Exception(() => invalid.SetDisplay(1600, 900, WindowDisplayMode.Fullscreen)));
            Assert.Equal(1920, invalid.Width);
            Assert.Equal(WindowDisplayMode.Windowed, invalid.WindowMode);
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
            var repo = new Rpg.Settings.SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            repo.SetResolution(1600, 900);
            repo.SetMasterVolume(0.4f);
            repo.SelectRole(1001);
            Assert.False(File.Exists(repo.RolePath));
            Assert.Throws<ArgumentException>(() => repo.SetHotbarVisible(1, false));
            repo.SetHotbarVisible(2, true);
            repo.SetHudPosition("portrait", 0.4f, 0.2f);
            Assert.NotEqual(0.4f, SettingsTestFixture.ReadRole(directory, 1001).HudPositions["portrait"].X);
            Assert.Equal(0.4f, repo.GetHudPosition("portrait").X);
            repo.SaveCurrentRole();
            repo.SelectRole(1002);
            Assert.False(repo.IsHotbarVisible(2));
            repo.SetHudPosition("portrait", 0.6f, 0.3f);
            repo.SelectRole(1001);
            Assert.True(repo.IsHotbarVisible(2));
            Assert.Equal(0.4f, repo.GetHudPosition("portrait").X);
            repo.ResetHudPositions();

            var reopened = new Rpg.Settings.SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            Assert.Equal(1600, reopened.Width);
            Assert.Equal(0.4f, reopened.MasterVolume);
            Assert.Equal(0.6f, SettingsTestFixture.ReadRole(directory, 1002).HudPositions["portrait"].X);
            Assert.Equal(RoleSetting.DefaultPositions()["portrait"].X,
                SettingsTestFixture.ReadRole(directory, 1001).HudPositions["portrait"].X);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    /// <summary>验收 client-settings：聊天尺寸兼容旧设置、延迟提交、角色隔离及默认恢复。</summary>
    [Fact]
    public void ChatSizeRemainsIsolatedUntilLayoutIsSaved()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-chat-size-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "roles"));
            File.WriteAllText(Path.Combine(directory, "roles", "1001.json"), "{}");
            var repo = new Rpg.Settings.SettingService(new EnvService(directory), new Rpg.Services.LogService(Console.WriteLine));
            repo.SelectRole(1001);
            Assert.Equal(DataHudSize.DefaultWidth, repo.GetChatSize().Width);
            repo.SetChatSize(480, 250);
            Assert.Equal("{}", File.ReadAllText(repo.RolePath));
            var copy = repo.GetChatSize();
            copy.Width = 600;
            Assert.Equal(480, repo.GetChatSize().Width);
            repo.SaveCurrentRole();
            repo.SelectRole(1002);
            Assert.Equal(DataHudSize.DefaultWidth, repo.GetChatSize().Width);
            repo.SelectRole(1001);
            Assert.Equal(480, repo.GetChatSize().Width);
            Assert.Equal(250, repo.GetChatSize().Height);
            repo.ResetHudPositions();
            Assert.Equal(DataHudSize.DefaultWidth, SettingsTestFixture.ReadRole(directory, 1001).ChatSize.Width);
            File.WriteAllText(Path.Combine(directory, "roles", "1003.json"), "{\"ChatSize\":{\"Width\":0,\"Height\":20}}");
            repo.SelectRole(1003);
            Assert.Equal(DataHudSize.DefaultHeight, repo.GetChatSize().Height);
            repo.Dispose();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

}

/// <summary>为 openspec/specs/client-config-map 的地图场景加载真实配置仓库。</summary>
internal static class ConfigTestFixture
{
    /// <summary>载入项目自带的地图配置。</summary>
    internal static ConfigService LoadDefault()
    {
        return new ConfigService(CreateEnv());
    }

    /// <summary>为 game-services 验收提供独立存档目录，配置使用正式资源路径。</summary>
    internal static EnvService CreateEnv(string settingsDirectory = "", string serverDirectory = "")
    {
        return new EnvService(settingsDirectory, serverDirectory);
    }

    /// <summary>从测试程序集目录向上寻找项目根目录。</summary>
    internal static string FindRoot()
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(typeof(ConfigTestFixture).Assembly.Location)!);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
    }
}

/// <summary>按 client-settings 的公开 JSON 存档格式读取验收结果。</summary>
internal static class SettingsTestFixture
{
    /// <summary>读取角色存档；文件尚未创建时使用约定的默认布局。</summary>
    internal static RoleSetting ReadRole(string directory, long roleId)
    {
        var path = Path.Combine(directory, "roles", $"{roleId}.json");
        return File.Exists(path)
            ? System.Text.Json.JsonSerializer.Deserialize<RoleSetting>(File.ReadAllText(path))!
            : new RoleSetting();
    }
}
