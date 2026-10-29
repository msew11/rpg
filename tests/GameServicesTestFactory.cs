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
using Rpg.ServerV2.Hosting;

namespace Rpg.Tests;

/// <summary>为 game-services 的隔离环境及替身传输验收组装实例，正式启动只走 Initialize。</summary>
internal static class GameServicesTestFactory
{
    /// <summary>以独立目录建立服务组，失败时回收测试创建的全部实例。</summary>
    internal static IDisposable Register(EnvService env) => Register(env, false);

    /// <summary>独立 V2 验收仍注册完整服务组，由 Net 拥有模拟服务端。</summary>
    internal static IDisposable RegisterV2(EnvService env) => Register(env, true);

    /// <summary>仅测试装配选择协议版本，生产无参启动保持旧链路。</summary>
    private static IDisposable Register(EnvService env, bool useV2)
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
            NetService net;
            if (useV2)
            {
                var server = new MockServerService(env, config);
                created.Add(server);
                net = new NetService(server, eventBus, log);
            }
            else net = new NetService(env, eventBus, config, log);
            created.Add(net);
            return Register(env, eventBus, config, setting, net);
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
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(net);
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
            return GameServices.Register(env, net.Log, eventBus, config, setting, net, session, world, animation, items);
        }
        catch
        {
            for (var i = created.Count - 1; i >= 0; i--) created[i].Dispose();
            throw;
        }
    }
}
