using System;
using System.Threading.Tasks;
using Rpg.Config;
using Rpg.Server.Modules.Common;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules;

/// <summary>宿主拥有的完整模块集合，区分正式固定装配与测试侧受控组合。</summary>
internal interface IServerModules : IActorLocator, IAsyncDisposable
{
    /// <summary>全部模块借用的所属配置。</summary>
    ConfigService Config { get; }
    /// <summary>模块是否提供完整多人调度处理器。</summary>
    bool SupportsMovement { get; }
    /// <summary>所有分片共用的运行时身份校验目录。</summary>
    ActorCallRegistry Calls { get; }
    /// <summary>已经执行但尚未共同落盘的地面物品转移。</summary>
    bool GroundItemsPending { get; set; }
    /// <summary>保存所有已启动分片，保留失败后的待写状态。</summary>
    Task FlushAsync();
}
