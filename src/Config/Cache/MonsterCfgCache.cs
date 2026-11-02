using System.IO;
using Godot;
using Rpg.Config.Data;

namespace Rpg.Config.Cache;

/// <summary>加载怪物配置并检查展示资源与地面锚点。</summary>
[CfgFile("monster.json")]
public sealed class MonsterCfgCache : MultiCfgCache<int, MonsterCfg>
{
    /// <summary>由配置服务统一拥有和释放。</summary>
    internal MonsterCfgCache() { }

    /// <summary>禁止缺图及画布外锚点进入可放置列表。</summary>
    protected override void Validate(MonsterCfg value)
    {
        if (string.IsNullOrWhiteSpace(value.Name) || !ResourceLoader.Exists(value.TexturePath) ||
            ResourceLoader.Load<Texture2D>(value.TexturePath) is not { } texture)
            throw new InvalidDataException($"怪物 {value.Id} 的名称或贴图无效：{value.TexturePath}");
        if (value.AnchorX < 0 || value.AnchorY < 0 || value.AnchorX >= texture.GetWidth() || value.AnchorY >= texture.GetHeight())
            throw new InvalidDataException($"怪物 {value.Id} 的地面锚点超出贴图画布");
    }
}
