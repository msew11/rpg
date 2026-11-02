using System;
using System.Globalization;
using System.Threading.Tasks;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>在地图邮箱内创建静态怪物并通知已有观察者。</summary>
internal sealed class CreateMonsterHandler : IActorHandler<WorldContext, CreateMonster, CreateMonsterRt>
{
    /// <summary>所有参数验证通过后一次提交，失败不修改地图缓存。</summary>
    public ValueTask<CreateMonsterRt> HandleAsync(WorldContext context, CreateMonster request)
    {
        if (!context.Config.Monster.Items.ContainsKey(request.MonsterTypeId))
            throw new ArgumentException("怪物配置不存在");
        if (!GroundDrop.Contains(context.Cache.Layout, request.X, request.Y))
            throw new ArgumentException("怪物坐标必须为地图内的有限坐标");
        var value = new DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 1,
            X = request.X, Y = request.Y, MonsterTypeId = request.MonsterTypeId.ToString(CultureInfo.InvariantCulture),
            Health = (int)context.Config.Monster.Items[request.MonsterTypeId].Attributes[2],
            MaxHealth = (int)context.Config.Monster.Items[request.MonsterTypeId].Attributes[2] };
        context.Cache.Objects.Commit(context.Cache.Objects.Value.Add(value));
        GroundItems.Mark(context, value);
        return ValueTask.FromResult(new CreateMonsterRt { Object = GroundItems.ToPb(value) });
    }
}
