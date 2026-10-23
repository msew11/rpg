using System;
using System.Collections.Generic;
using System.Numerics;
using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server.Data;

namespace Rpg.Server;

/// <summary>处理 protobuf 请求、延迟移动确认及独立服务端持久化。</summary>
public sealed class MockServer
{
    /// <summary>角色 JSON 存储边界。</summary>
    private readonly RepoServerRole _roles;
    /// <summary>当前会话待确认的移动。</summary>
    private readonly List<DataPendingMove> _pending = [];
    /// <summary>已成功落盘的当前角色状态。</summary>
    private DataServerRole? _current;
    /// <summary>当前模拟位置，尚未确认为已保存状态。</summary>
    private Vector2 _position;
    /// <summary>最近的移动目标。</summary>
    private Vector2 _target;
    /// <summary>当前允许处理的会话代次。</summary>
    private long _generation;
    /// <summary>是否已通过演示账号验证。</summary>
    private bool _authenticated;
    /// <summary>本会话最近收到的移动序号。</summary>
    private long _lastAction;
    /// <summary>服务器发出的独立 protobuf 信封字节。</summary>
    public event Action<byte[]>? Outgoing;

    /// <summary>绑定独立服务端存储目录。</summary>
    public MockServer(string directory) => _roles = new RepoServerRole(directory);

    /// <summary>结束角色模拟并更新代次；保留账号登录供返回选角使用。</summary>
    public void ResetSession(long generation)
    {
        _generation = generation;
        _current = null;
        _pending.Clear();
        _lastAction = 0;
    }

    /// <summary>解析客户端字节并将允许的请求交给处理方法。</summary>
    public void Receive(byte[] packet)
    {
        var envelope = MessageCodec.ReadEnvelope(packet);
        if (envelope.Generation != _generation) return;
        switch (MessageCodec.ReadPayload(envelope, true))
        {
            case Login login: HandleLogin(login); break;
            case SelectRole select: HandleSelectRole(select); break;
            case Move move: HandleMove(move); break;
        }
    }

    /// <summary>按既有模拟速度推进位置，延迟 0.45 秒后保存并返回移动确认。</summary>
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (_current is null) return;
        var distance = Vector2.Distance(_position, _target);
        var step = (float)(180 * delta);
        _position = distance <= step || distance == 0 ? _target : _position + (_target - _position) / distance * step;
        for (var index = 0; index < _pending.Count;)
        {
            var pending = _pending[index] with { Remaining = _pending[index].Remaining - delta };
            if (pending.Remaining > 0) { _pending[index++] = pending; continue; }
            DataServerRole candidate;
            try
            {
                candidate = _current with { X = _position.X + 3, Y = _position.Y,
                    Revision = checked(_current.Revision + 1), AcknowledgedAction = pending.ActionId };
                _roles.Save(candidate);
            }
            catch (Exception error)
            {
                _position = _target = new Vector2(_current.X, _current.Y);
                var failed = _pending.ToArray();
                _pending.Clear();
                foreach (var move in failed) Send(new MoveRt { RoleId = _current.RoleId, ActionId = move.ActionId,
                    Error = $"角色存档保存失败：{error.Message}" });
                return;
            }
            _current = candidate;
            _pending.RemoveAt(index);
            Send(new MoveRt { Success = true, RoleId = candidate.RoleId, ActionId = pending.ActionId, State = ToMessage(candidate) });
        }
    }

    /// <summary>主动提交当前角色的新状态，保存成功后才发推送；失败由调用方观察异常。</summary>
    public void PublishRoleState(DataServerRole update)
    {
        if (_current is null || update.RoleId != _current.RoleId) throw new InvalidOperationException("只能推送当前角色状态");
        var candidate = update with { Revision = checked(_current.Revision + 1), AcknowledgedAction = _current.AcknowledgedAction };
        _roles.Save(candidate);
        _current = candidate;
        _position = _target = new Vector2(candidate.X, candidate.Y);
        Send(new RoleStateNotice { State = ToMessage(candidate) });
    }

    /// <summary>验证演示账号并从服务端文件读取独立角色摘要。</summary>
    private void HandleLogin(Login request)
    {
        _authenticated = false;
        if (request.Username != "demo" || request.Password != "demo")
        {
            Send(new LoginRt { Error = "用户名或密码错误" });
            return;
        }
        var response = new LoginRt();
        try
        {
            foreach (var roleId in RepoServerRole.RoleIds) response.Roles.Add(ToSummary(_roles.Load(roleId)));
            response.Success = _authenticated = true;
        }
        catch (Exception error) { response.Error = error.Message; response.Roles.Clear(); }
        Send(response);
    }

    /// <summary>加载选中角色并重置本次会话的移动序号。</summary>
    private void HandleSelectRole(SelectRole request)
    {
        var response = new SelectRoleRt { RoleId = request.RoleId };
        try
        {
            if (!_authenticated) throw new InvalidOperationException("请先登录");
            var role = _roles.Load(request.RoleId) with { AcknowledgedAction = 0 };
            _pending.Clear();
            _lastAction = 0;
            _position = _target = new Vector2(role.X, role.Y);
            _current = role;
            response.Success = true;
            response.State = ToMessage(role);
        }
        catch (Exception error) { response.Error = error.Message; }
        Send(response);
    }

    /// <summary>验证角色、坐标及序号后排队，成功确认由时间推进产生。</summary>
    private void HandleMove(Move request)
    {
        if (_current is null || request.RoleId != _current.RoleId || request.ActionId <= _lastAction ||
            !float.IsFinite(request.X) || !float.IsFinite(request.Y))
        {
            Send(new MoveRt { RoleId = request.RoleId, ActionId = request.ActionId, Error = "无效的角色、坐标或移动序号" });
            return;
        }
        _lastAction = request.ActionId;
        _target = new Vector2(request.X, request.Y);
        _pending.Add(new DataPendingMove(request.ActionId, 0.45));
    }

    /// <summary>登录只返回选角页所需的角色摘要。</summary>
    private static RoleSummaryPb ToSummary(DataServerRole role) => new()
    {
        RoleId = role.RoleId, Name = role.Name, Level = role.Level,
        Gender = role.Gender == ServerRoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female
    };

    /// <summary>将完整角色世界状态复制为协议对象，不向客户端共享服务端模型。</summary>
    private static RoleWorldStatePb ToMessage(DataServerRole role) => new()
    {
        RoleId = role.RoleId, Name = role.Name, Level = role.Level, Health = role.Health, MaxHealth = role.MaxHealth,
        Mana = role.Mana, MaxMana = role.MaxMana, Experience = role.Experience, NextExperience = role.NextExperience,
        X = role.X, Y = role.Y, Gender = role.Gender == ServerRoleGender.Male ? RoleGenderPb.Male : RoleGenderPb.Female,
        Revision = role.Revision, AcknowledgedAction = role.AcknowledgedAction
    };

    /// <summary>冻结响应为当前代次的字节交给传输层。</summary>
    private void Send(IMessage response) => Outgoing?.Invoke(MessageCodec.Encode(response, _generation, false));

    /// <summary>记录等待延迟确认的移动。</summary>
    /// <param name="ActionId">客户端提交的意图序号。</param>
    /// <param name="Remaining">距离确认剩余的模拟秒数。</param>
    private sealed record DataPendingMove(long ActionId, double Remaining);
}
