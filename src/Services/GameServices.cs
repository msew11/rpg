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
    /// <summary>先于消费者就绪、最后释放的唯一事件服务。</summary>
    private static EventService? _events;
    /// <summary>当前启动共享的事件域，不依赖网络生命周期。</summary>
    public static EventService Events => _events is { IsDisposed: false } events
        ? events : throw new InvalidOperationException("GameServices 事件服务尚未初始化或已经释放");

    /// <summary>只建立事件域，完整启动与隔离事件测试共用此创建位置。</summary>
    internal static EventService InitializeEvents()
    {
        if (_events is { IsDisposed: false }) throw new InvalidOperationException("事件服务已初始化");
        return _events = new EventService();
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

    /// <summary>在入口内部创建各服务，调用方只提供运行目录与可选传输创建边界。</summary>
    public static IDisposable Initialize(string settingsDirectory, string serverDirectory, Func<string, NetService>? createNet = null)
    {
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        var config = new ConfigService();
        SettingService? setting = null;
        NetService? net = null;
        try
        {
            setting = new SettingService(settingsDirectory);
            net = createNet is null ? new NetService(serverDirectory) : createNet(serverDirectory);
            return Initialize(config, setting, net);
        }
        catch
        {
            try { net?.Dispose(); }
            finally { try { setting?.Dispose(); } finally { config.Dispose(); } }
            throw;
        }
    }

    /// <summary>成功后接管显式服务并建立其依赖；失败时传入实例仍由调用方释放。</summary>
    internal static IDisposable Initialize(ConfigService config, SettingService setting, NetService net)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(net);
        ObjectDisposedException.ThrowIf(net.IsDisposed, net);
        if (_current is not null) throw new InvalidOperationException("GameServices 已初始化，请先释放原服务句柄");
        _ = config.MapConfig;
        _ = setting.Width;
        var events = InitializeEvents();
        try
        {
            var registration = new Registration(config, setting, net, events);
            _current = registration;
            return registration;
        }
        catch { events.Dispose(); _events = null; throw; }
    }

    /// <summary>一次启动的所有权句柄，固定依赖图直接以构造顺序表达。</summary>
    private sealed class Registration : IDisposable
    {
        /// <summary>本注册拥有的唯一事件域。</summary>
        private readonly EventService _events;
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
        public AnimationService Animation { get; } = new();
        /// <summary>防止旧句柄重复释放后续注册。</summary>
        private bool _disposed;

        /// <summary>先构造依赖，再构造消费者；内部失败只回收新增对象。</summary>
        public Registration(ConfigService config, SettingService setting, NetService net, EventService events)
        {
            _events = events;
            Config = config;
            Setting = setting;
            Net = net;
            Session = new SessionService(net);
            try { World = new WorldService(config, setting, Session); }
            catch { try { Session.Dispose(); } finally { Animation.Dispose(); } throw; }
        }

        /// <summary>先撤销业务入口，再逆依赖清理并最后撤销事件入口；单个服务失败不阻止其余清理。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_current, this)) _current = null;
            var errors = new List<Exception>();
            foreach (var service in new IService[] { Animation, World, Session, Net, Setting, Config, _events })
            {
                try { service.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (ReferenceEquals(GameServices._events, _events)) GameServices._events = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("客户端服务释放失败", errors);
        }
    }
}
