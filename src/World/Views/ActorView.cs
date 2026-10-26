using Godot;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>根据只读玩家快照组合身体、头部和默认武器，不持有或改变 ECS 组件。</summary>
public partial class ActorView : Node2D
{
    /// <summary>围绕角色定位点镜像整个身体的容器。</summary>
    private readonly Node2D _facingRoot = new() { Name = "Facing" };
    /// <summary>由 Godot 独立推进动画时间的身体节点。</summary>
    private readonly AnimatedSprite2D _body = new()
        { Name = "Body", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>独立头部图层，由身体帧变化更新挂接位置。</summary>
    private readonly Sprite2D _head = new()
        { Name = "Head", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>角色实际右手的逐帧挂点，独立承载武器并参与角色内部遮挡。</summary>
    private readonly Node2D _rightHand = new() { Name = "RightHand" };
    /// <summary>默认测试长剑的独立持握图层，共用身体帧而不启动另一动画时钟。</summary>
    private readonly Sprite2D _weapon = new()
        { Name = "Weapon", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>上次读取的快照，用来区分动作变化和普通位置变化。</summary>
    private DataActorState? _previous;
    /// <summary>角色实体原点的 GM 调试标记。</summary>
    private readonly EntityAnchorView _anchor = new() { Name = "EntityAnchor", Visible = false };

    /// <summary>创建身体节点；镜像以玩家位置为原点，保持锚点稳定。</summary>
    public override void _Ready()
    {
        AddChild(_facingRoot);
        _facingRoot.AddChild(_body);
        _facingRoot.AddChild(_head);
        _facingRoot.AddChild(_rightHand);
        _rightHand.AddChild(_weapon);
        AddChild(_anchor);
        _body.FrameChanged += UpdateAttachments;
        _body.AnimationChanged += UpdateAttachments;
        if (_previous is { } state)
        {
            _previous = null;
            Present(state);
        }
    }

    /// <summary>设置当前角色锚点的显隐和颜色。</summary>
    public void SetAnchorMarker(bool visible, Color color)
    {
        _anchor.Visible = visible;
        _anchor.SetMarkerColor(color);
    }

    /// <summary>同步位置、动作和朝向；同动作持续呈现时不重启播放。</summary>
    public void Present(DataActorState state)
    {
        Position = Utils.IsometricProjection.ToScreen(state.Position);
        if (!IsNodeReady()) { _previous = state; return; }
        var changed = _previous is null || _previous.RoleId != state.RoleId ||
            _previous.Gender != state.Gender || _previous.HeadId != state.HeadId || _previous.Motion != state.Motion ||
            _previous.MotionVersion != state.MotionVersion;
        var (direction, mirrored) = AnimationService.ResolveDirection(state.Facing);
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
        UpdateAttachments();
    }
    /// <summary>头部、武器与身体共用方向和时间，逐帧更新挂接位置及武器遮挡。</summary>
    private void UpdateAttachments()
    {
        if (!IsInsideTree() || !GameServices.IsInitialized ||
            _previous is not { } state || _body.SpriteFrames is null) return;
        var body = GameServices.Animation.Get(state.Gender, state.Motion);
        // 换动作时属性信号可能先于新快照更新，等 Present 结束后统一同步。
        if (!ReferenceEquals(body.Frames, _body.SpriteFrames)) return;
        var (direction, mirrored) = AnimationService.ResolveDirection(state.Facing);
        if (_body.Animation != $"dir{direction}") return;
        var head = GameServices.Animation.GetHead(state.Gender, state.HeadId);
        var count = body.Frames.GetFrameCount(_body.Animation);
        _head.Texture = head.Frames[direction];
        _head.Position = -body.Anchor + body.HeadAttachments[direction * count + _body.Frame] - head.Anchors[direction];
        var weapon = GameServices.Animation.GetDefaultWeapon(state.Gender, state.Motion);
        var weaponCount = weapon.Frames.Length / 5;
        // 同源动作逐帧一致；复用动作按进度映射，短动作末帧也取武器末帧。
        var weaponFrame = count <= 1 ? 0 : Mathf.RoundToInt((float)_body.Frame * (weaponCount - 1) / (count - 1));
        var index = direction * weaponCount + weaponFrame;
        _weapon.Texture = weapon.Frames[index];
        // 镜像会互换左右手，先选原画的另一只手，再统一镜像为角色实际右手。
        var handPoints = mirrored ? body.LeftHandAttachments : body.RightHandAttachments;
        _rightHand.Position = -body.Anchor + handPoints[direction * count + _body.Frame];
        _weapon.Position = -weapon.Anchors[index];
        // 只调整角色内部绘制顺序，避免武器 Z 值跨过按实体坐标排序的其他角色和物品。
        var layer = weapon.Layers[index];
        var behind = direction switch
        {
            3 => true,  // 背面身体遮挡剑身。
            4 => false, // 正面剑身遮挡身体。
            // 侧面默认远手在后；非零动作偏置在改用原画另一只手时反转。
            _ => layer == 0 ? !mirrored : mirrored ? layer < 0 : layer > 0
        };
        var childIndex = behind ? 0 : _facingRoot.GetChildCount() - 1;
        if (_rightHand.GetIndex() != childIndex) _facingRoot.MoveChild(_rightHand, childIndex);
    }

}
