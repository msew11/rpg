using System;
using Godot;

namespace Rpg.Session;

/// <summary>隔离登录、选角和会话状态传输来源的客户端契约。</summary>
public interface IStateSource
{
    /// <summary>状态源接收到新的权威角色数据时发出通知。</summary>
    event Action<DataRoleState>? StateReceived;

    /// <summary>核对用户凭据并返回可选角色。</summary>
    DataLoginResult Login(string username, string password);

    /// <summary>为指定角色建立新的会话。</summary>
    GameSession SelectRole(long roleId);

    /// <summary>发送带序号的移动意图。</summary>
    void SubmitMove(long actionId, Vector2 target);

    /// <summary>推进本地状态源；真实网络适配器可仅处理消息队列。</summary>
    void Advance(double delta, DataRole role);
}
