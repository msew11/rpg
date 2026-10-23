using System;
using Godot;
using Rpg.Const;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>统一拥有身体动画资源缓存，供多个角色节点复用。</summary>
public sealed class AnimationService : IService
{
    /// <summary>本次启动独占的动画仓库，释放时撤销持有。</summary>
    private RepoActorAnimation? _repo = new();

    /// <summary>取得指定性别和动作对应的原生动画资源。</summary>
    public DataActorAnimation Get(RoleGender gender, RoleMotion motion) =>
        (_repo ?? throw new ObjectDisposedException(nameof(AnimationService))).Get(gender, motion);

    /// <summary>把逻辑朝向转换为五方向动画与镜像标记。</summary>
    public (int Direction, bool Mirrored) ResolveDirection(Vector2 facing) => RepoActorAnimation.ResolveDirection(facing);

    /// <summary>撤销缓存引用，原生资源由 Godot 引用计数管理。</summary>
    public void Dispose() => _repo = null;
}
