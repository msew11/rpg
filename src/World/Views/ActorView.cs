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
    /// <summary>镜像前源右手的逐帧挂点，独立承载武器并参与角色内部遮挡。</summary>
    private readonly Node2D _rightHand = new() { Name = "RightHand" };
    /// <summary>主手装备的独立持握图层，共用身体帧而不启动另一动画时钟。</summary>
    private readonly Sprite2D _weapon = new()
        { Name = "Weapon", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>镜像前源左手的独立挂点，随整个组合镜像。</summary>
    private readonly Node2D _leftHand = new() { Name = "LeftHand" };
    /// <summary>源左手图层，显示副手盾牌或主手成对武器的左手部分。</summary>
    private readonly Sprite2D _offWeapon = new()
        { Name = "Weapon", Centered = false, TextureFilter = TextureFilterEnum.Nearest };
    /// <summary>当前解析后的身体动作，与逻辑请求独立。</summary>
    private Rpg.Const.RoleMotion _motion;
    /// <summary>身体、头、副手、主手在角色内排序的节点。</summary>
    private Node2D[] _parts = [];
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
        _parts = [_body, _head, _leftHand, _rightHand];
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
        var motion = GameServices.Animation.ResolveMotion(state.Gender, state.Motion,
            Appearance(state.MainHandId), Appearance(state.OffHandId));
        var changed = _previous is null || _previous.RoleId != state.RoleId ||
            _previous.Gender != state.Gender || _previous.HeadId != state.HeadId || _motion != motion ||
            _previous.MotionVersion != state.MotionVersion;
        var (direction, mirrored) = AnimationService.ResolveDirection(state.Facing);
        var name = $"dir{direction}";
        _facingRoot.Scale = new Vector2(mirrored ? -1 : 1, 1);
        if (changed)
        {
            var resource = GameServices.Animation.Get(state.Gender, motion);
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
        _motion = motion;
        _previous = state;
        UpdateAttachments();
    }
    /// <summary>头部、武器与身体共用方向和时间，逐帧更新挂接位置及武器遮挡。</summary>
    private void UpdateAttachments()
    {
        if (!IsInsideTree() || !GameServices.IsInitialized ||
            _previous is not { } state || _body.SpriteFrames is null) return;
        var body = GameServices.Animation.Get(state.Gender, _motion);
        // 换动作时属性信号可能先于新快照更新，等 Present 结束后统一同步。
        if (!ReferenceEquals(body.Frames, _body.SpriteFrames)) return;
        var (direction, mirrored) = AnimationService.ResolveDirection(state.Facing);
        if (_body.Animation != $"dir{direction}") return;
        var head = GameServices.Animation.GetHead(state.Gender, state.HeadId);
        var count = body.Frames.GetFrameCount(_body.Animation);
        _head.Texture = head.Frames[direction];
        _head.Position = -body.Anchor + body.HeadAttachments[direction * count + _body.Frame] - head.Anchors[direction];
        UpdateWeapon(state.MainHandId, true, _rightHand, _weapon, state, body, count, direction);
        var leftItemId = state.OffHandId;
        if (state.MainHandId != 0)
        {
            var cfg = GameServices.Config.Item.Items[state.MainHandId];
            if (Rpg.Utils.EquipmentSlotUtil.HasPairedAppearance(cfg.Category, cfg.SubCategory)) leftItemId = state.MainHandId;
        }
        UpdateWeapon(leftItemId, false, _leftHand, _offWeapon, state, body, count, direction);
        SortParts(state, direction, mirrored);
    }

    /// <summary>按固定源手挂接完整武器，身体帧对原生帧数取余，整体镜像不换手。</summary>
    private void UpdateWeapon(int itemId, bool mainHand, Node2D hand, Sprite2D sprite,
        DataActorState state, DataActorAnimation body, int count, int direction)
    {
        sprite.Visible = itemId != 0;
        if (itemId == 0) { sprite.Texture = null; return; }
        var weapon = GameServices.Animation.GetWeapon(Appearance(itemId), state.Gender, _motion, mainHand);
        var weaponCount = weapon.Frames.Length / 5;
        var index = direction * weaponCount + _body.Frame % weaponCount;
        sprite.Texture = weapon.Frames[index];
        var handPoints = mainHand ? body.RightHandAttachments : body.LeftHandAttachments;
        hand.Position = -body.Anchor + handPoints[direction * count + _body.Frame];
        sprite.Position = -weapon.Anchors[index];
    }

    /// <summary>按原方向基础顺序进行升序稳定排序，仅影响同一实体内部的绘制次序。</summary>
    private void SortParts(DataActorState state, int direction, bool mirrored)
    {
        var data = GameServices.Animation.Composition;
        System.Span<int> order = stackalloc int[4];
        for (var index = 0; index < 4; index++) order[index] = data.PartAt(direction, mirrored, index);
        for (var index = 1; index < 4; index++)
        {
            var part = order[index];
            var value = data.Layer(state.Gender, _motion, part, direction, _body.Frame);
            var previous = index - 1;
            while (previous >= 0 && data.Layer(state.Gender, _motion, order[previous], direction, _body.Frame) > value)
            {
                order[previous + 1] = order[previous];
                previous--;
            }
            order[previous + 1] = part;
        }
        for (var index = 0; index < 4; index++)
            if (_parts[order[index]].GetIndex() != index) _facingRoot.MoveChild(_parts[order[index]], index);
    }

    /// <summary>由已确认装备道具查找外观，空槽不加载资源。</summary>
    private static int Appearance(int itemId) => itemId == 0 ? 0 :
        GameServices.Config.Equipment.Items[GameServices.Config.Item.Items[itemId].RelatedId].AppearanceId;
}
