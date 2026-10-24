using System;

namespace Rpg.Services;

/// <summary>约定业务服务拥有其状态，并在所属启动流程结束时释放。</summary>
public interface IService : IDisposable
{
    /// <summary>服务是否已结束生命周期。</summary>
    bool IsDisposed { get; }
}
