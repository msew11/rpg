using System.Threading.Tasks;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Role;

/// <summary>按角色身份串行执行的模块，暂不包含游戏状态或 Handler。</summary>
internal sealed class RoleActor : ModuleActor<RoleContext>
{
    /// <summary>本分片固定角色身份。</summary>
    private readonly long _roleId;
    /// <summary>绑定专用处理器表与角色身份。</summary>
    internal RoleActor(long roleId, ActorHandlers<RoleContext> handlers) : base(handlers) => _roleId = roleId;
    /// <summary>按需构造角色工作区，不读取旧角色文件。</summary>
    protected override ValueTask<RoleContext> CreateContextAsync() => ValueTask.FromResult(new RoleContext(_roleId));
}
