using System;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;

namespace Rpg.Tests;

/// <summary>正式异步协议下的场景等待，服务于角色管理与各物品 UI 验收。</summary>
internal static class SmokeNetwork
{
    /// <summary>账号响应接纳后才继续界面断言，保留真实帧分发和超时失败。</summary>
    internal static async Task WaitForAccount(Node node)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (GameServices.IsInitialized && GameServices.Session.AccountRequestPending)
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("等待账号响应超时");
            GameServices.Net.Tick(0);
            await node.ToSignal(node.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
