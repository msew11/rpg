using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.Common;

/// <summary>借用所属服务的分片定位能力，只返回消息引用，不拥有目录生命周期。</summary>
public interface IActorLocator
{
    /// <summary>按规范账号键定位，不触发初始化。</summary>
    ActorRef Login(string username);
    /// <summary>按全局角色身份定位，不触发初始化。</summary>
    ActorRef Role(long roleId);
    /// <summary>按地图身份定位，不触发初始化。</summary>
    ActorRef World(int mapId);
}
