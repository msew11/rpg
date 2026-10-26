using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Rpg.Config;
using Rpg.Net;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Views;
using Rpg.Utils;

namespace Rpg.Services;

/// <summary>以固定入口持有客户端服务，按显式依赖顺序初始化并逆序释放。</summary>
public static class GameServices
{
    /// <summary>本次启动的整体注册，释放前禁止替换。</summary>
    private static Registration? _current;
    /// <summary>完整服务组是否已经注册。</summary>
    public static bool IsInitialized => _current is not null;
    /// <summary>当前完整注册，不将其他服务的可用性绑定到网络状态。</summary>
    private static Registration Current
    {
        get
        {
            var current = _current ?? throw new InvalidOperationException("GameServices 尚未初始化，请先由启动流程注册服务");
            return current;
        }
    }
    /// <summary>本次启动统一的资源与持久化目录。</summary>
    public static EnvService Env => Current.Env;
    /// <summary>本次启动统一的控制台日志。</summary>
    public static LogService Log => Current.Log;
    /// <summary>客户端只读配置。</summary>
    public static ConfigService Config => Current.Config;
    /// <summary>当前系统与角色本地设置。</summary>
    public static SettingService Setting => Current.Setting;
    /// <summary>当前完整注册中的事件域。</summary>
    public static EventBusService EventBus => Current.EventBus;
    /// <summary>固定帧网络请求与消息分发。</summary>
    public static NetService Net => Current.Net;
    /// <summary>账号与当前角色会话。</summary>
    public static SessionService Session => Current.Session;
    /// <summary>地图、输入意图和角色世界。</summary>
    public static WorldService World => Current.World;
    /// <summary>跨表现节点共享的动画资源缓存。</summary>
    public static AnimationService Animation => Current.Animation;
    /// <summary>跨背包、装备、地面和图鉴共享的物品资源。</summary>
    public static ItemAssetService Items => Current.Items;

    /// <summary>在同一方法内按固定依赖顺序创建全部服务，失败时逆序回收。</summary>
    public static IDisposable Initialize()
    {
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        var created = new List<IService>();
        try
        {
            var env = new EnvService();
            created.Add(env);
            var log = new LogService(env);
            created.Add(log);
            var eventBus = new EventBusService();
            created.Add(eventBus);
            var config = new ConfigService(env);
            created.Add(config);
            var setting = new SettingService(env, log);
            created.Add(setting);
            var net = new NetService(env, eventBus, config, log);
            created.Add(net);
            var session = new SessionService(net);
            created.Add(session);
            var world = new WorldService(env, config, setting, session);
            created.Add(world);
            var animation = new AnimationService();
            created.Add(animation);
            var items = new ItemAssetService();
            created.Add(items);
            return Register(env, log, eventBus, config, setting, net, session, world, animation, items);
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            for (var i = created.Count - 1; i >= 0; i--)
            {
                try { created[i].Dispose(); }
                catch (Exception cleanupError) { errors.Add(cleanupError); }
            }
            if (errors.Count > 1) throw new AggregateException("客户端服务初始化失败", errors);
            throw;
        }
    }

    /// <summary>验证并接管完整服务组，仅注册现成实例，不创建服务。</summary>
    internal static IDisposable Register(EnvService env, LogService log, EventBusService eventBus, ConfigService config,
        SettingService setting, NetService net, SessionService session, WorldService world, AnimationService animation, ItemAssetService items)
    {
        foreach (var service in new IService[] { env, log, eventBus, config, setting, net, session, world, animation, items })
        {
            ArgumentNullException.ThrowIfNull(service);
            ObjectDisposedException.ThrowIf(service.IsDisposed, service);
        }
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        return _current = new Registration(env, log, eventBus, config, setting, net, session, world, animation, items);
    }

    /// <summary>一次启动的所有权句柄，只保存服务实例并负责释放。</summary>
    private sealed class Registration : IDisposable
    {
        /// <summary>最先就绪、最后释放的环境服务。</summary>
        public EnvService Env { get; }
        /// <summary>后于日志消费者释放的输出服务。</summary>
        public LogService Log { get; }
        /// <summary>本注册拥有的唯一事件域。</summary>
        public EventBusService EventBus { get; }
        /// <summary>先就绪的配置服务。</summary>
        public ConfigService Config { get; }
        /// <summary>先就绪的设置服务。</summary>
        public SettingService Setting { get; }
        /// <summary>会话与世界借用的网络服务。</summary>
        public NetService Net { get; }
        /// <summary>先于世界创建的账号服务。</summary>
        public SessionService Session { get; }
        /// <summary>依赖配置、设置与账号的世界服务。</summary>
        public WorldService World { get; }
        /// <summary>与账号无关的表现资源服务。</summary>
        public AnimationService Animation { get; }
        /// <summary>本注册拥有的物品目录和图集缓存。</summary>
        public ItemAssetService Items { get; }
        /// <summary>防止旧句柄重复释放后续注册。</summary>
        private bool _disposed;

        /// <summary>保存已按依赖顺序创建的完整服务组。</summary>
        public Registration(EnvService env, LogService log, EventBusService eventBus, ConfigService config,
            SettingService setting, NetService net, SessionService session, WorldService world, AnimationService animation, ItemAssetService items)
        {
            Env = env;
            Log = log;
            EventBus = eventBus;
            Config = config;
            Setting = setting;
            Net = net;
            Session = session;
            World = world;
            Animation = animation;
            Items = items;
        }

        /// <summary>先撤销全部入口，再严格逆构造顺序清理；单个服务失败不阻止其余清理。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_current, this)) _current = null;
            var errors = new List<Exception>();
            foreach (var service in new IService[] { Items, Animation, World, Session, Net, Setting, Config, EventBus, Log, Env })
            {
                try { service.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("客户端服务释放失败", errors);
        }
    }
}
