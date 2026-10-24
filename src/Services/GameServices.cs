using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Rpg.Config;
using Rpg.Net;
using Rpg.Session;
using Rpg.Settings;
using Rpg.World;
using Rpg.World.Views;

namespace Rpg.Services;

/// <summary>以固定入口持有客户端服务，按显式依赖顺序初始化并逆序释放。</summary>
public static class GameServices
{
    /// <summary>本次启动的整体注册，释放前禁止替换。</summary>
    private static Registration? _current;
    /// <summary>先于消费者就绪、在消费者之后释放的唯一事件服务。</summary>
    private static EventService? _events;
    /// <summary>当前启动共享的事件域，不依赖网络生命周期。</summary>
    public static EventService Events => _events is { IsDisposed: false } events
        ? events : throw new InvalidOperationException("GameServices 事件服务尚未初始化或已经释放");

    /// <summary>为隔离测试注册已创建的事件域，不承担服务构造。</summary>
    internal static EventService RegisterEvents(EventService events)
    {
        ArgumentNullException.ThrowIfNull(events);
        ObjectDisposedException.ThrowIf(events.IsDisposed, events);
        if (_events is { IsDisposed: false }) throw new InvalidOperationException("事件服务已初始化");
        return _events = events;
    }

    /// <summary>全部入口是否已注册且网络尚未释放。</summary>
    public static bool IsInitialized => _current is not null && !_current.Net.IsDisposed;
    /// <summary>当前有效注册；未启动和网络提前释放均明确报错。</summary>
    private static Registration Current
    {
        get
        {
            var current = _current ?? throw new InvalidOperationException("GameServices 尚未初始化，请先由启动流程注册服务");
            ObjectDisposedException.ThrowIf(current.Net.IsDisposed, current.Net);
            return current;
        }
    }
    /// <summary>本次启动统一的资源与持久化目录。</summary>
    public static EnvironmentService Environment => Current.Environment;
    /// <summary>客户端只读配置。</summary>
    public static ConfigService Config => Current.Config;
    /// <summary>当前系统与角色本地设置。</summary>
    public static SettingService Setting => Current.Setting;
    /// <summary>固定帧网络请求与消息分发。</summary>
    public static NetService Net => Current.Net;
    /// <summary>账号与当前角色会话。</summary>
    public static SessionService Session => Current.Session;
    /// <summary>地图、输入意图和角色世界。</summary>
    public static WorldService World => Current.World;
    /// <summary>跨表现节点共享的动画资源缓存。</summary>
    public static AnimationService Animation => Current.Animation;

    /// <summary>在同一方法内按固定依赖顺序创建全部服务，失败时逆序回收。</summary>
    public static IDisposable Initialize()
    {
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        if (_events is { IsDisposed: false }) throw new InvalidOperationException("事件服务已初始化");
        var created = new List<IService>();
        try
        {
            var environment = new EnvironmentService();
            created.Add(environment);
            var events = new EventService();
            created.Add(events);
            _events = events;
            var config = new ConfigService(environment);
            created.Add(config);
            var setting = new SettingService(environment);
            created.Add(setting);
            var net = new NetService(environment);
            created.Add(net);
            var session = new SessionService(net);
            created.Add(session);
            var world = new WorldService(config, setting, session);
            created.Add(world);
            var animation = new AnimationService();
            created.Add(animation);
            return Register(environment, events, config, setting, net, session, world, animation);
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };
            for (var i = created.Count - 1; i >= 0; i--)
            {
                try { created[i].Dispose(); }
                catch (Exception cleanupError) { errors.Add(cleanupError); }
            }
            _events = null;
            if (errors.Count > 1) throw new AggregateException("客户端服务初始化失败", errors);
            throw;
        }
    }

    /// <summary>验证并接管完整服务组，仅注册现成实例，不创建服务。</summary>
    internal static IDisposable Register(EnvironmentService environment, EventService events, ConfigService config,
        SettingService setting, NetService net, SessionService session, WorldService world, AnimationService animation)
    {
        foreach (var service in new IService[] { environment, events, config, setting, net, session, world, animation })
        {
            ArgumentNullException.ThrowIfNull(service);
            ObjectDisposedException.ThrowIf(service.IsDisposed, service);
        }
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        if (!ReferenceEquals(_events, events)) throw new InvalidOperationException("注册服务必须使用当前事件域");
        return _current = new Registration(environment, events, config, setting, net, session, world, animation);
    }

    /// <summary>一次启动的所有权句柄，只保存服务实例并负责释放。</summary>
    private sealed class Registration : IDisposable
    {
        /// <summary>最先就绪、最后释放的环境服务。</summary>
        public EnvironmentService Environment { get; }
        /// <summary>本注册拥有的唯一事件域。</summary>
        public EventService Events { get; }
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
        /// <summary>防止旧句柄重复释放后续注册。</summary>
        private bool _disposed;

        /// <summary>保存已按依赖顺序创建的完整服务组。</summary>
        public Registration(EnvironmentService environment, EventService events, ConfigService config,
            SettingService setting, NetService net, SessionService session, WorldService world, AnimationService animation)
        {
            Environment = environment;
            Events = events;
            Config = config;
            Setting = setting;
            Net = net;
            Session = session;
            World = world;
            Animation = animation;
        }

        /// <summary>先撤销业务入口，再逆依赖清理并最后撤销事件入口；单个服务失败不阻止其余清理。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_current, this)) _current = null;
            var errors = new List<Exception>();
            foreach (var service in new IService[] { Animation, World, Session, Net, Setting, Config, Events, Environment })
            {
                try { service.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (ReferenceEquals(GameServices._events, Events)) GameServices._events = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("客户端服务释放失败", errors);
        }
    }
}
