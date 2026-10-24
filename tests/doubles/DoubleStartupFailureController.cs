using System;
using System.IO;
using Rpg.Net;
using Rpg.Config;
using Rpg.Services;
using Rpg.Settings;
using Rpg.UI;

namespace Rpg.Tests.Doubles;

/// <summary>服务于 openspec/specs/game-services 的注册成功后启动失败清理场景。</summary>
public partial class DoubleStartupFailureController : MainController
{
    /// <summary>在初次登录页重置网络时抛错的测试传输。</summary>
    private readonly DoubleStartupTransport _transport = new();
    /// <summary>供验收确认已被释放的网络实例。</summary>
    public NetService? CreatedNet { get; private set; }
    /// <summary>底层传输是否随启动失败被释放。</summary>
    public bool TransportDisposed => _transport.IsDisposed;

    /// <summary>注册测试服务组，登录页重置网络时触发启动失败。</summary>
    protected override IDisposable InitializeServices()
    {
        var environment = new EnvironmentService(Path.Combine(Path.GetTempPath(), "rpg-startup-" + Guid.NewGuid()));
        var config = new ConfigService(environment);
        var setting = new SettingService(environment);
        CreatedNet = new NetService(_transport);
        return GameServices.Register(environment, config, setting, CreatedNet);
    }

    /// <summary>第一次会话重置成功，后续重置触发受控的启动异常。</summary>
    private sealed class DoubleStartupTransport : INetTransport
    {
        /// <summary>是否已执行传输清理。</summary>
        public bool IsDisposed { get; private set; }
        /// <summary>不产生服务器消息，无需保存监听。</summary>
        public event Action<byte[]>? Received { add { } remove { } }
        /// <summary>启动失败前不应提交任何请求。</summary>
        public void Send(byte[] packet) => throw new InvalidOperationException("启动失败前不应发送请求");
        /// <summary>不生成延迟消息。</summary>
        public void Tick(double delta) { }
        /// <summary>只允许网络构造时的首代重置。</summary>
        public void ResetSession(long generation)
        {
            if (generation > 1) throw new IOException("模拟注册后启动失败");
        }
        /// <summary>标记底层已关闭。</summary>
        public void Dispose() => IsDisposed = true;
    }
}
