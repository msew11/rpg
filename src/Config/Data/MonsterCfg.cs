using Rpg.Config.Cache;

namespace Rpg.Config.Data;

/// <summary>怪物静态展示配置，不包含尚未确定的玩法属性。</summary>
public sealed class MonsterCfg
{
    /// <summary>怪物配置的正整型主键，地图边界使用其十进制文本标识。</summary>
    [CfgKey]
    public int Id { get; init; }
    /// <summary>列表与 GM 反馈使用的中文名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>随项目发布的透明贴图资源路径。</summary>
    public string TexturePath { get; init; } = "";
    /// <summary>原始画布中地面锚点的横坐标。</summary>
    public int AnchorX { get; init; }
    /// <summary>原始画布中地面锚点的纵坐标。</summary>
    public int AnchorY { get; init; }
}
