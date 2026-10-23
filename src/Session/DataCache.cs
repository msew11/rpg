using System;

namespace Rpg.Session;

/// <summary>组合当前会话各功能模块的服务器权威缓存。</summary>
public sealed class DataCache : IDisposable
{
    /// <summary>当前角色模块缓存。</summary>
    public RoleCache Role { get; }

    /// <summary>在发送选角前建立功能模块的优先监听。</summary>
    public DataCache(long roleId, EventSystem events) => Role = new RoleCache(roleId, events);

    /// <summary>统一释放组合模块的订阅。</summary>
    public void Dispose() => Role.Dispose();
}
