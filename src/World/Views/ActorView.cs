using Godot;
using Rpg.Services;

namespace Rpg.World.Views;

/// <summary>根据只读玩家快照组合身体、头部和装备武器，不持有或改变 ECS 组件。</summary>
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
    /// <summary>主手装备的独立持握图层，共用身体帧而不启动另一动画时钟。</summary>
    private readonly Sprite2D _weapon = new()
        { Name = "Weapon", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>实际左手的独立挂点，镜像时与身体原画手别互换。</summary>
    private readonly Node2D _leftHand = new() { Name = "LeftHand" };
    /// <summary>副手装备层，与主手共享身体时钟。</summary>
    private readonly Sprite2D _offWeapon = new()
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
        _facingRoot.AddChild(_leftHand);
        _leftHand.AddChild(_offWeapon);
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
        UpdateWeapon(state.MainHandId, true, _rightHand, _weapon, state, body, count, direction, mirrored);
        UpdateWeapon(state.OffHandId, false, _leftHand, _offWeapon, state, body, count, direction, mirrored);
    }

    /// <summary>解析显式配置外观并更新一只实际手的挂点、逐帧纹理和内部遮挡。</summary>
    private void UpdateWeapon(int itemId, bool mainHand, Node2D hand, Sprite2D sprite, DataActorState state,
        DataActorAnimation body, int count, int direction, bool mirrored)
    {
        sprite.Visible = itemId != 0;
        if (itemId == 0) { sprite.Texture = null; return; }
        var item = GameServices.Config.Item.Items[itemId];
        var appearance = GameServices.Config.Equipment.Items[item.RelatedId].AppearanceId;
        var weapon = GameServices.Animation.GetWeapon(appearance, state.Gender, state.Motion);
        var weaponCount = weapon.Frames.Length / 5;
        var weaponFrame = count <= 1 ? 0 : Mathf.RoundToInt((float)_body.Frame * (weaponCount - 1) / (count - 1));
        var index = direction * weaponCount + weaponFrame;
        sprite.Texture = weapon.Frames[index];
        // 镜像前先交换原画手别，确保实际左右手语义不变。
        var handPoints = mainHand != mirrored ? body.RightHandAttachments : body.LeftHandAttachments;
        hand.Position = -body.Anchor + handPoints[direction * count + _body.Frame];
        sprite.Position = -weapon.Anchors[index];
        var layer = weapon.Layers[index];
        var farHand = mainHand ? !mirrored : mirrored;
        var behind = direction switch
        {
            3 => true,
            4 => false,
            _ => layer == 0 ? farHand : mirrored ? layer < 0 : layer > 0
        };
        // 只在角色内部排列，保持整个角色与其他实体的坐标排序。
        var childIndex = behind ? 0 : _facingRoot.GetChildCount() - 1;
        if (hand.GetIndex() != childIndex) _facingRoot.MoveChild(hand, childIndex);
    }
}
