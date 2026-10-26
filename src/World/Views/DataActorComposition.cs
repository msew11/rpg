using Godot;
using Rpg.Const;

namespace Rpg.World.Views;

/// <summary>原客户端八方向基础部件次序与男女完整身体动作层次。</summary>
[GlobalClass]
public partial class DataActorComposition : Resource
{
    /// <summary>按SW/W/NW/N/NE/E/SE/S排列，每方向存身体0、头1、副手2、主手3的次序。</summary>
    [Export] public int[] BaseOrder { get; set; } = [];
    /// <summary>性别、13身体动作、4部件、5方向、50身体帧依次展平的来源层次。</summary>
    [Export] public int[] Layers { get; set; } = [];

    /// <summary>读取原客户端基础次序，镜像保留源部件身份。</summary>
    public int PartAt(int direction, bool mirrored, int index)
    {
        var row = direction == 4 ? 7 : mirrored ? 6 - direction : direction;
        return BaseOrder[row * 4 + index];
    }

    /// <summary>按身体实际动作与帧读取层次，独立于武器是否回退站立。</summary>
    public int Layer(RoleGender gender, RoleMotion motion, int part, int direction, int frame) =>
        Layers[(((((gender == RoleGender.Female ? 1 : 0) * 13 + (int)motion) * 4 + part) * 5 + direction) * 50) + frame];
}
