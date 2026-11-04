using Rpg.Session.Data;

namespace Rpg.World.Components;

/// <summary>单次预测或确认攻击及防止旧通知复活的版本。</summary>
internal sealed class DataAttackComponent
{
    /// <summary>当前动作快照，起手后固定本次时序与目标。</summary>
    public DataAttack? Current;
    /// <summary>当前本人起手请求是否仍待响应，与动画是否结束独立。</summary>
    public bool Pending;
    /// <summary>当前动作是否已经得到权威确认。</summary>
    public bool Confirmed;
    /// <summary>本地取消的攻击编号上界。</summary>
    public ulong CancelledThrough;
    /// <summary>最后接纳的权威攻击起手时间。</summary>
    public long AuthorityStart;
    /// <summary>最后接纳的权威攻击编号。</summary>
    public ulong AuthorityId;
    /// <summary>最后接纳的权威攻击是否已取消。</summary>
    public bool AuthorityCancelled;

    /// <summary>建立没有攻击及权威版本的状态。</summary>
    public DataAttackComponent()
    {
        Current = null;
        Pending = false;
        Confirmed = false;
        CancelledThrough = 0;
        AuthorityStart = 0;
        AuthorityId = 0;
        AuthorityCancelled = false;
    }
}
