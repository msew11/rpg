using System.Collections.Immutable;
using System.Threading.Tasks;
using Rpg.Server.Modules.World.Data;

namespace Rpg.Server.Modules.World.Storage;

/// <summary>地图文件边界；同地图由所属 Actor 串行调用，不缓存业务状态。</summary>
public interface IWorldRepo
{
    /// <summary>完整加载指定地图，缺失清单返回空快照。</summary>
    ValueTask<DataWorldSnapshot> LoadAsync(DataWorldLayout layout);
    /// <summary>提交最终变化块；成功返回即表示清单已提交。</summary>
    ValueTask SaveAsync(DataWorldLayout layout, DataWorldSnapshot snapshot, ImmutableHashSet<DataWorldChunk> dirtyChunks);
}
