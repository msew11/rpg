using System;
using System.Collections.Generic;
using Godot;
using Rpg.Const;

namespace Rpg.World.Views;

/// <summary>表现层按性别和动作加载并复用项目内的身体动画。</summary>
public sealed class RepoActorAnimation
{
    /// <summary>当前表现生命周期中已使用的动作资源。</summary>
    private readonly Dictionary<(RoleGender, RoleMotion), DataActorAnimation> _animations = new();

    /// <summary>加载对应动作；资源缺失时给出明确的路径诊断。</summary>
    public DataActorAnimation Get(RoleGender gender, RoleMotion motion)
    {
        if (_animations.TryGetValue((gender, motion), out var animation)) return animation;
        var body = gender switch
        {
            RoleGender.Male => "male",
            RoleGender.Female => "female",
            _ => throw new ArgumentOutOfRangeException(nameof(gender))
        };
        if (!Enum.IsDefined(motion)) throw new ArgumentOutOfRangeException(nameof(motion));
        var path = $"res://assets/characters/{body}/{motion.ToString().ToLowerInvariant()}.tres";
        animation = GD.Load<DataActorAnimation>(path)
            ?? throw new InvalidOperationException($"角色动画资源加载失败：{path}");
        _animations.Add((gender, motion), animation);
        return animation;
    }

    /// <summary>将逻辑朝向转换为五方向资源索引及水平镜像标记。</summary>
    public static (int Direction, bool Mirrored) ResolveDirection(Vector2 facing)
    {
        var screen = Utils.IsometricProjection.ToScreen(facing);
        var octant = Mathf.PosMod(Mathf.RoundToInt(screen.Angle() / (Mathf.Pi / 4)), 8);
        return octant switch
        {
            0 => (1, true),  // 右。
            1 => (0, true),  // 右下。
            2 => (4, false), // 下。
            3 => (0, false), // 左下。
            4 => (1, false), // 左。
            5 => (2, false), // 左上。
            6 => (3, false), // 上。
            _ => (2, true)   // 右上。
        };
    }
}
