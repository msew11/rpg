using System;
using Rpg.Config.Data;
using Rpg.Services;

namespace Rpg.Config;

/// <summary>拥有本次启动的已校验配置，隐藏配置加载仓库。</summary>
public sealed class ConfigService : IService
{
    /// <summary>本次启动独占使用的配置仓库。</summary>
    private RepoGameConfig? _repo;

    /// <summary>接管已成功加载的配置，拒绝未就绪仓库。</summary>
    public ConfigService(RepoGameConfig repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        _ = repo.MapConfig;
        _repo = repo;
    }

    /// <summary>只读取得地图配置，释放后禁止读取。</summary>
    public MapConfig MapConfig => (_repo ?? throw new ObjectDisposedException(nameof(ConfigService))).MapConfig;

    /// <summary>撤销对本次配置的持有。</summary>
    public void Dispose() => _repo = null;
}
