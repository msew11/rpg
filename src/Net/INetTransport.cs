using System;

namespace Rpg.Net;

/// <summary>隔离网络管理器与具体字节传输，所有方法在主线程调用。</summary>
public interface INetTransport : IDisposable
{
    /// <summary>接收一份完整且不再修改的服务器信封字节。</summary>
    event Action<byte[]>? Received;
    /// <summary>将客户端信封发送到对端。</summary>
    void Send(byte[] packet);
    /// <summary>推进传输或本地模拟服务端。</summary>
    void Tick(double delta);
    /// <summary>结束旧角色工作并切换到新的会话代次。</summary>
    void ResetSession(long generation);
}
