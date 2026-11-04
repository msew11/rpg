namespace Rpg.World.Components;

/// <summary>怪物已接纳的权威生命，客户端不计算伤害。</summary>
internal sealed class DataHealthComponent
{
    /// <summary>当前生命。</summary>
    public int Current;
    /// <summary>生命上限。</summary>
    public int Maximum;

    /// <summary>从已验证快照建立生命数据。</summary>
    public DataHealthComponent(int current, int maximum)
    {
        Current = current;
        Maximum = maximum;
    }
}
