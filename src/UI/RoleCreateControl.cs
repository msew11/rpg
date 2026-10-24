using System;
using Godot;
using Rpg.Const;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.UI;

/// <summary>创建角色的名字、性别、头型与独立八方向预览，不创建 ECS 世界。</summary>
public partial class RoleCreateControl : VBoxContainer
{
    /// <summary>提交当前草稿，由主控制器调用会话服务。</summary>
    public event Action<string, RoleGender, int>? Submitted;
    /// <summary>放弃草稿并返回选角列表。</summary>
    public event Action? Cancelled;
    /// <summary>角色名字输入。</summary>
    private readonly LineEdit _name = new() { Name = "RoleName", PlaceholderText = "角色名字（1—16 个字符）", MaxLength = 16 };
    /// <summary>身体性别选项。</summary>
    private readonly OptionButton _gender = new() { Name = "GenderChoice" };
    /// <summary>当前性别的全部头型编号。</summary>
    private readonly OptionButton _head = new() { Name = "HeadChoice" };
    /// <summary>固定大小的预览窗口，身体与头部分层呈现。</summary>
    private readonly PanelContainer _preview = new() { Name = "RolePreview", CustomMinimumSize = new Vector2(360, 220), ClipContents = true };
    /// <summary>独立于世界和角色缓存的预览节点。</summary>
    private readonly ActorView _actor = new() { Name = "PreviewActor", Scale = new Vector2(1.7f, 1.7f) };
    /// <summary>请求状态或服务器失败信息。</summary>
    private readonly Label _feedback = new() { Name = "CreateFeedback", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    /// <summary>屏幕八方向索引，初始面向下方。</summary>
    private int _direction = 2;

    /// <summary>建立创建表单，并将旋转按钮放在预览窗口下方。</summary>
    public override void _Ready()
    {
        AddChild(_name);
        _gender.AddItem("男", 0);
        _gender.AddItem("女", 1);
        AddChild(_gender);
        for (var id = 1; id <= 74; id++) _head.AddItem($"头型 {id:00} / 74", id);
        var heads = new HBoxContainer();
        AddChild(heads);
        heads.AddChild(Button("上一款", "PreviousHead", () => SelectHead(-1)));
        _head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heads.AddChild(_head);
        heads.AddChild(Button("下一款", "NextHead", () => SelectHead(1)));
        AddChild(_preview);
        _preview.AddChild(_actor);
        var rotation = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        AddChild(rotation);
        rotation.AddChild(Button("逆时针旋转", "RotateCounterclockwise", () => Rotate(-1)));
        rotation.AddChild(Button("顺时针旋转", "RotateClockwise", () => Rotate(1)));
        AddChild(_feedback);
        AddChild(Button("创建角色", "SubmitCreate", () => Submitted?.Invoke(_name.Text, (RoleGender)_gender.Selected, _head.GetSelectedId())));
        AddChild(Button("返回角色列表", "CancelCreate", () => Cancelled?.Invoke()));
        _gender.ItemSelected += _ => Present();
        _head.ItemSelected += _ => Present();
        _preview.Resized += Present;
        Present();
    }

    /// <summary>等待响应时禁用表单，失败后保留草稿供修改重试。</summary>
    public void SetBusy(bool busy, string message)
    {
        _name.Editable = !busy;
        foreach (var node in FindChildren("*", "BaseButton", true, false)) ((BaseButton)node).Disabled = busy;
        _feedback.Text = message;
    }

    /// <summary>头型前后循环，切换时保留预览朝向。</summary>
    private void SelectHead(int step)
    {
        _head.Select(Mathf.PosMod(_head.Selected + step, 74));
        Present();
    }

    /// <summary>在屏幕坐标系顺逆转动 45 度，八次回到原方向。</summary>
    private void Rotate(int step)
    {
        _direction = Mathf.PosMod(_direction + step, 8);
        Present();
    }

    /// <summary>以预览窗口中的固定脚底位置呈现当前草稿，不访问世界服务。</summary>
    private void Present()
    {
        if (!IsNodeReady()) return;
        var facing = Vector2.FromAngle(_direction * Mathf.Pi / 4);
        _actor.Present(new DataActorState(0, (RoleGender)_gender.Selected,
            IsometricProjection.ToLogical(new Vector2(_preview.Size.X / 2, _preview.Size.Y - 20)),
            RoleMotion.Stand, IsometricProjection.ToLogical(facing), 0, _head.GetSelectedId()));
    }

    /// <summary>创建可被界面验收定位的按钮并绑定单次点击。</summary>
    private static Button Button(string text, string name, Action action)
    {
        var button = new Button { Text = text, Name = name, CustomMinimumSize = new Vector2(0, 34) };
        button.Pressed += action;
        return button;
    }
}
