using System;

namespace Rpg.Services;

/// <summary>共享服务释放状态和检查，具体资源仍由各服务自行清理。</summary>
public abstract class Service : IService
{
    /// <summary>服务是否已释放；由具体服务按其清理顺序标记。</summary>
    public bool IsDisposed { get; protected set; }

    /// <summary>拒绝已结束生命周期的服务继续处理业务。</summary>
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    /// <summary>由具体服务幂等清理资源，并标记释放状态。</summary>
    public abstract void Dispose();
}
