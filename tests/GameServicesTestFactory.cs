using System;
using System.Collections.Generic;
using Rpg.Config;
using Rpg.Net;
using Rpg.Services;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Views;
using Rpg.Utils;
using Rpg.Server.Hosting;

namespace Rpg.Tests;

/// <summary>为 game-services 的隔离环境及替身传输验收组装实例，正式启动只走 Initialize。</summary>
internal static class GameServicesTestFactory
{
    /// <summary>隔离目录注册正式服务器与进程内字节传输。</summary>
    internal static IDisposable Register(EnvService env)
    {
        ArgumentNullException.ThrowIfNull(env);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        if (GameServices.IsInitialized) throw new InvalidOperationException("GameServices 已初始化");
        var created = new List<IService> { env };
        try
        {
            var log = new LogService(env);
            created.Add(log);
            var eventBus = new EventBusService();
            created.Add(eventBus);
            var config = new ConfigService(env);
            created.Add(config);
            var setting = new SettingService(env, log);
            created.Add(setting);
            var server = MockServerService.CreateInProcess(env, config);
            created.Add(server);
            var net = NetService.CreateInProcess(server, eventBus, log);
            created.Add(net);
            return Register(env, eventBus, config, setting, server, net);
        }
        catch
        {
            for (var i = created.Count - 1; i >= 0; i--) created[i].Dispose();
            throw;
        }
    }

    /// <summary>成功后接管测试显式提供的依赖，失败时仅回收本方法创建的服务。</summary>
    internal static IDisposable Register(EnvService env, EventBusService eventBus, ConfigService config, SettingService setting, NetService net)
    {
        var server = new MockServerService(env, config);
        try { return Register(env, eventBus, config, setting, server, net); }
        catch { server.Dispose(); throw; }
    }

    /// <summary>接管测试显式创建的服务器服务和网络，失败时保留传入实例所有权。</summary>
    internal static IDisposable Register(EnvService env, EventBusService eventBus, ConfigService config, SettingService setting, MockServerService server, NetService net)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(net);
        ArgumentNullException.ThrowIfNull(server);
        ObjectDisposedException.ThrowIf(env.IsDisposed, env);
        ObjectDisposedException.ThrowIf(net.IsDisposed, net);
        if (GameServices.IsInitialized) throw new InvalidOperationException("GameServices 已初始化");
        _ = config.Map.Items[config.Basic.Value.InitialMapId];
        _ = setting.Width;
        var created = new List<IService>();
        try
        {
            var session = new SessionService(net);
            created.Add(session);
            var world = new WorldService(env, config, setting, session);
            created.Add(world);
            var animation = new AnimationService();
            created.Add(animation);
            var items = new ItemAssetService();
            created.Add(items);
            return GameServices.Register(env, net.Log, eventBus, config, setting, server, net, session, world, animation, items);
        }
        catch
        {
            for (var i = created.Count - 1; i >= 0; i--) created[i].Dispose();
            throw;
        }
    }
}
