using System;
using Godot;

namespace Rpg.World.Components;

/// <summary>持续攻击意图与已经提交的走近目的地。</summary>
internal sealed class DataCombatIntentComponent
{
    /// <summary>持续攻击目标的服务器身份。</summary>
    public Guid? Target;
    /// <summary>上一次由走近系统提交的目的地，避免重复发送。</summary>
    public Vector2? ChaseDestination;

    /// <summary>以没有攻击目标的状态建立意图。</summary>
    public DataCombatIntentComponent()
    {
        Target = null;
        ChaseDestination = null;
    }
}
