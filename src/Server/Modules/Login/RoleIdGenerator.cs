using System.Threading;
using System.Threading.Tasks;
using Rpg.Server.Modules.Login.Storage;

namespace Rpg.Server.Modules.Login;

/// <summary>与账号无关的角色编号生成器，只发放已经持久化预留的正整数。</summary>
internal sealed class RoleIdGenerator
{
    /// <summary>串行化不同账号的取号与编号段切换。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>负责预留编号段的持久化仓库。</summary>
    private readonly RepoRoleIdSequence _repo;
    /// <summary>当前编号段最后发放的编号。</summary>
    private long _lastIssued;
    /// <summary>当前已持久化预留的最大编号。</summary>
    private long _reservedThrough;

    /// <summary>为一套服务器数据目录建立独立生成器，不预先执行 IO。</summary>
    internal RoleIdGenerator(string directory) => _repo = new(directory);

    /// <summary>互斥发放下一个编号，耗尽当前编号段时先持久化新段。</summary>
    internal async ValueTask<long> NextAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_lastIssued == _reservedThrough)
                (_lastIssued, _reservedThrough) = await _repo.ReserveAsync().ConfigureAwait(false);
            return ++_lastIssued;
        }
        finally { _gate.Release(); }
    }
}
