using System;
using System.IO;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.Modules.Common.Storage;
using Rpg.Server.Protocol;

namespace Rpg.Server.Modules.Login.Storage;

/// <summary>保存全服角色编号预留上界，先持久化再交付可用编号段。</summary>
internal sealed class RepoRoleIdSequence
{
    /// <summary>全新数据目录的首个角色编号。</summary>
    internal const long FirstId = 1003;
    /// <summary>每轮最多预留的编号数量，降低逐角色刷盘开销。</summary>
    private const int BlockSize = 256;
    /// <summary>存储预留上界的独立文件。</summary>
    private readonly string _path;

    /// <summary>绑定服务器专用目录，不读取或迁移角色文件。</summary>
    internal RepoRoleIdSequence(string directory) => _path = Path.Combine(Path.GetFullPath(directory), "role_id_sequence.json");

    /// <summary>独占读取并原子推进上界；锁竞争或 IO 失败时拒绝，调用方可以重试。</summary>
    internal async ValueTask<(long Previous, long Through)> ReserveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        long previous;
        try { previous = await StorageJson.ReadAsync<long>(_path).ConfigureAwait(false); }
        catch (FileNotFoundException) { previous = FirstId - 1; }
        if (previous < FirstId - 1) throw new InvalidDataException("角色编号预留上界无效");
        if (previous == long.MaxValue)
            throw new ServerRequestException(ErrorCodePb.InvalidRequest, "角色编号已耗尽");
        var through = previous + Math.Min(BlockSize, long.MaxValue - previous);
        await StorageJson.WriteAsync(_path, StorageJson.Encode(through)).ConfigureAwait(false);
        return (previous, through);
    }
}
