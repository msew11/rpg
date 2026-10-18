using System;
using System.Collections.Generic;
using Godot;

namespace Rpg.Session;

/// <summary>提供固定账号、角色与延迟移动确认的本地状态源。</summary>
public sealed class MockStateSource : IStateSource
{
    /// <summary>演示账号可选择的角色。</summary>
    private readonly List<DataRole> _roles =
    [
        new(1001, "晨星", 5, 86, 100, 42, 60, 240, 500, new Vector2(16016, 16016)),
        new(1002, "流光", 3, 64, 80, 75, 90, 120, 300, new Vector2(16144, 16080))
    ];
    /// <summary>尚未到达确认时间的移动意图。</summary>
    private readonly List<PendingMove> _pending = [];
    /// <summary>模拟服务端当前位置。</summary>
    private Vector2 _authorityPosition;
    /// <summary>模拟服务端当前目标。</summary>
    private Vector2 _authorityTarget;
    /// <summary>模拟服务端状态版本。</summary>
    private long _revision;

    /// <summary>模拟源产生新的权威角色状态。</summary>
    public event Action<DataRoleState>? StateReceived;

    /// <summary>核对演示凭据并返回角色列表。</summary>
    public DataLoginResult Login(string username, string password) =>
        username == "demo" && password == "demo"
            ? new DataLoginResult(true, "", _roles)
            : new DataLoginResult(false, "用户名或密码错误", []);

    /// <summary>按角色 ID 建立新的模拟会话。</summary>
    public GameSession SelectRole(long roleId)
    {
        var role = _roles.Find(candidate => candidate.RoleId == roleId)
            ?? throw new ArgumentException("角色不存在", nameof(roleId));
        _pending.Clear();
        _revision = 0;
        _authorityPosition = role.Position;
        _authorityTarget = role.Position;
        return new GameSession(role, this);
    }

    /// <summary>接收客户端的目标位置和意图序号。</summary>
    public void SubmitMove(long actionId, Vector2 target)
    {
        _authorityTarget = target;
        _pending.Add(new PendingMove(actionId, 0.45));
    }

    /// <summary>推进模拟服务端，并在延迟后返回确认位置。</summary>
    public void Advance(double delta, DataRole role)
    {
        _authorityPosition = _authorityPosition.MoveToward(_authorityTarget, (float)(180 * delta));
        for (var index = 0; index < _pending.Count;)
        {
            var pending = _pending[index] with { Remaining = _pending[index].Remaining - delta };
            if (pending.Remaining > 0)
            {
                _pending[index] = pending;
                index++;
                continue;
            }
            _pending.RemoveAt(index);
            // 为演示校正链路，确认位置在模拟服务端位置上保留一个小偏差。
            var correction = _authorityPosition + new Vector2(3, 0);
            StateReceived?.Invoke(new DataRoleState(role, correction, ++_revision, pending.ActionId));
        }
    }

    /// <summary>尚待发送确认的移动意图。</summary>
    private sealed record PendingMove(long ActionId, double Remaining);
}
