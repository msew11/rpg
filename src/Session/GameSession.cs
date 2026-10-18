using System;

namespace Rpg.Session;

/// <summary>绑定选中角色、模拟源和会话数据缓存。</summary>
public sealed class GameSession : IDisposable
{
    /// <summary>本会话使用的状态来源。</summary>
    private readonly IStateSource _source;

    /// <summary>本会话的角色。</summary>
    public DataRole Role { get; }
    /// <summary>本会话的服务器数据缓存。</summary>
    public DataCache Cache { get; }

    /// <summary>初始化角色缓存并订阅状态源。</summary>
    public GameSession(DataRole role, IStateSource source)
    {
        Role = role;
        Cache = new DataCache(role);
        _source = source;
        _source.StateReceived += OnStateReceived;
    }

    /// <summary>每帧推进本地模拟状态源。</summary>
    public void Advance(double delta) => _source.Advance(delta, Role);

    /// <summary>将移动意图交给模拟状态源。</summary>
    public void SubmitMove(long actionId, Godot.Vector2 target) => _source.SubmitMove(actionId, target);

    /// <summary>断开旧会话的数据订阅。</summary>
    public void Dispose() => _source.StateReceived -= OnStateReceived;

    /// <summary>将状态源更新写入会话缓存。</summary>
    private void OnStateReceived(DataRoleState state)
    {
        if (state.Role.RoleId == Role.RoleId) Cache.Apply(state);
    }
}
