using System;
using System.Collections.Generic;
using Rpg.Config;
using Rpg.Net;
using Rpg.Services;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>为 game-services 的隔离环境及替身传输验收组装实例，正式启动只走 Initialize。</summary>
internal static class GameServicesTestFactory
{
    /// <summary>以独立目录建立服务组，失败时回收测试创建的全部实例。</summary>
    internal static IDisposable Register(EnvironmentService environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ObjectDisposedException.ThrowIf(environment.IsDisposed, environment);
        if (GameServices.IsInitialized) throw new InvalidOperationException("GameServices 已初始化");
        var created = new List<IService> { environment };
        try
        {
            var events = new EventService();
            created.Add(events);
            var config = new ConfigService(environment);
            created.Add(config);
            var setting = new SettingService(environment);
            created.Add(setting);
            var net = new NetService(environment, events);
            created.Add(net);
            return Register(environment, events, config, setting, net);
        }
        catch
        {
            for (var i = created.Count - 1; i >= 0; i--) created[i].Dispose();
            throw;
        }
    }

    /// <summary>成功后接管测试显式提供的依赖，失败时仅回收本方法创建的服务。</summary>
    internal static IDisposable Register(EnvironmentService environment, EventService events, ConfigService config, SettingService setting, NetService net)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(net);
        ObjectDisposedException.ThrowIf(environment.IsDisposed, environment);
        ObjectDisposedException.ThrowIf(net.IsDisposed, net);
        if (GameServices.IsInitialized) throw new InvalidOperationException("GameServices 已初始化");
        _ = config.MapConfig;
        _ = setting.Width;
        var created = new List<IService>();
        try
        {
            var session = new SessionService(net);
            created.Add(session);
            var world = new WorldService(config, setting, session);
            created.Add(world);
            var animation = new AnimationService();
            created.Add(animation);
            return GameServices.Register(environment, events, config, setting, net, session, world, animation);
        }
        catch
        {
            for (var i = created.Count - 1; i >= 0; i--) created[i].Dispose();
            throw;
        }
    }
}
