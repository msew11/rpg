using System;
using System.Threading;

namespace Rpg.ServerV2.Connections;

/// <summary>所有连接共用的待发送字节预算。</summary>
internal sealed class OutputBudget
{
    /// <summary>全局待发送字节数。</summary>
    private long _bytes;
    /// <summary>尝试预留，失败立即归还。</summary>
    internal bool Reserve(int bytes)
    { if (Interlocked.Add(ref _bytes, bytes) <= 64L * 1024 * 1024) return true; Release(bytes); return false; }
    /// <summary>发送或清理后归还预算。</summary>
    internal void Release(int bytes) => Interlocked.Add(ref _bytes, -bytes);
}
