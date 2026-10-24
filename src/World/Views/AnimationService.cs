using System;
using System.Collections.Generic;
using Godot;
using Rpg.Const;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>表现层按性别和动作加载并复用项目内的身体动画。</summary>
public sealed class AnimationService : Service
{
    /// <summary>当前表现生命周期中已使用的动作资源。</summary>
    private readonly Dictionary<(RoleGender, RoleMotion), DataActorAnimation> _animations = new();

    /// <summary>独立头部按性别和编号缓存，不复制身体动作。</summary>
    private readonly Dictionary<(RoleGender, int), DataActorHead> _heads = new();

    /// <summary>加载对应动作；资源缺失时给出明确的路径诊断。</summary>
    public DataActorAnimation Get(RoleGender gender, RoleMotion motion)
    {
        ThrowIfDisposed();
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

    /// <summary>加载独立头部；编号 0 兼容旧协议默认头型。</summary>
    public DataActorHead GetHead(RoleGender gender, int headId)
    {
        ThrowIfDisposed();
        if (headId == 0) headId = 1;
        if (headId is < 1 or > 74) throw new ArgumentOutOfRangeException(nameof(headId));
        if (_heads.TryGetValue((gender, headId), out var head)) return head;
        var body = gender switch
        {
            RoleGender.Male => "male",
            RoleGender.Female => "female",
            _ => throw new ArgumentOutOfRangeException(nameof(gender))
        };
        var path = $"res://assets/characters/heads/{body}/{headId}.tres";
        head = GD.Load<DataActorHead>(path) ?? throw new InvalidOperationException($"头部资源加载失败：{path}");
        if (head.Frames.Length != 5 || head.Anchors.Length != 5)
            throw new InvalidOperationException($"头部方向数据不完整：{path}");
        _heads.Add((gender, headId), head);
        return head;
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
    /// <summary>撤销资源缓存，原生资源仍由 Godot 引用计数管理。</summary>
    public override void Dispose() { IsDisposed = true; _animations.Clear(); _heads.Clear(); }

}
