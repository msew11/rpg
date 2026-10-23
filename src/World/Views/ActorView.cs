using Godot;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>根据只读玩家快照播放身体动画，不持有或改变 ECS 组件。</summary>
public partial class ActorView : Node2D
{
    /// <summary>围绕角色定位点镜像整个身体的容器。</summary>
    private readonly Node2D _facingRoot = new() { Name = "Facing" };
    /// <summary>由 Godot 独立推进动画时间的身体节点。</summary>
    private readonly AnimatedSprite2D _body = new()
        { Name = "Body", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>上次读取的快照，用来区分动作变化和普通位置变化。</summary>
    private DataActorState? _previous;

    /// <summary>创建身体节点；镜像以玩家位置为原点，保持锚点稳定。</summary>
    public override void _Ready()
    {
        AddChild(_facingRoot);
        _facingRoot.AddChild(_body);
        if (_previous is { } state)
        {
            _previous = null;
            Present(state);
        }
    }

    /// <summary>同步位置、动作和朝向；同动作持续呈现时不重启播放。</summary>
    public void Present(DataActorState state)
    {
        Position = Utils.IsometricProjection.ToScreen(state.Position);
        if (!IsNodeReady()) { _previous = state; return; }
        var changed = _previous is null || _previous.RoleId != state.RoleId ||
            _previous.Gender != state.Gender || _previous.Motion != state.Motion ||
            _previous.MotionVersion != state.MotionVersion;
        var (direction, mirrored) = GameServices.Animation.ResolveDirection(state.Facing);
        var name = $"dir{direction}";
        _facingRoot.Scale = new Vector2(mirrored ? -1 : 1, 1);
        if (changed)
        {
            var resource = GameServices.Animation.Get(state.Gender, state.Motion);
            _body.Stop();
            _body.SpriteFrames = resource.Frames;
            _body.Position = -resource.Anchor;
            _body.Play(name);
        }
        else if (_body.Animation != name)
        {
            var frame = _body.Frame;
            var progress = _body.FrameProgress;
            var playing = _body.IsPlaying();
            _body.Animation = name;
            _body.SetFrameAndProgress(frame, progress);
            if (playing) _body.Play();
        }
        _previous = state;
    }
}
