namespace Rpg.Net;

/// <summary>本地服务端传输在角色离开前同步提交待写数据的可选能力。</summary>
public interface IServerWriteBackTransport
{
    /// <summary>提交服务端已接纳的全部待写数据，失败时保留待写状态。</summary>
    void FlushServerWrites();
}
