using System;
using Godot;
using Rpg.Const;

namespace Rpg.UI;

/// <summary>右侧平铺外观选项，男女各自保存草稿，在中央预览下方发起命名创建。</summary>
public partial class RoleCreateControl : RoleScreenControl
{
    /// <summary>命名确认后提交当前性别的角色草稿。</summary>
    public event Action<string, RoleGender, int>? Submitted;
    /// <summary>放弃创建并返回角色列表。</summary>
    public event Action? Cancelled;
    /// <summary>当前正在编辑的身体性别。</summary>
    private RoleGender _gender = RoleGender.Male;
    /// <summary>男女独立的头型编号，切换不相互覆盖。</summary>
    private readonly int[] _headIds = [AvatarControl.FirstPortraitHeadId, AvatarControl.FirstPortraitHeadId];
    /// <summary>男女独立的屏幕八方向预览索引。</summary>
    private readonly int[] _directions = [RolePreviewControl.DefaultDirection, RolePreviewControl.DefaultDirection];
    /// <summary>男女独立的名字草稿，取消或请求失败时保留。</summary>
    private readonly string[] _names = ["", ""];
    /// <summary>当前是否正在等待服务器创建响应。</summary>
    private bool _busy;
    /// <summary>头型平铺网格，只展示有完整头像对应关系的款式。</summary>
    private readonly GridContainer _heads = new() { Name = "HeadGrid", Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    /// <summary>头型列表的滚动窗口。</summary>
    private readonly ScrollContainer _scroll = new() { Name = "HeadList", SizeFlagsVertical = SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    /// <summary>用于展示当前选中头型的标题。</summary>
    private readonly Label _headTitle = new() { Name = "HeadTitle" };
    /// <summary>名字输入窗口，确认才发送请求。</summary>
    private readonly ConfirmationDialog _nameDialog = new() { Name = "RoleNameDialog", Title = "输入角色名",
        OkButtonText = "确认创建", CancelButtonText = "取消", DialogHideOnOk = false };
    /// <summary>命名窗口的输入控件。</summary>
    private readonly LineEdit _name = new() { Name = "RoleName", PlaceholderText = "角色名字（1—16 个字符）", MaxLength = 16 };
    /// <summary>命名窗口内部的失败反馈，避免被模态窗口遮挡。</summary>
    private readonly Label _nameFeedback = new() { Name = "CreateFeedback", CustomMinimumSize = new Vector2(300, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
    /// <summary>男女选择器的互斥分组。</summary>
    private readonly ButtonGroup _genders = new();

    /// <summary>建立右侧性别按钮、可选头型网格以及中央创建操作。</summary>
    public override void _Ready()
    {
        base._Ready();
        RightContent.AddChild(new Label { Text = "性别" });
        var genders = new HBoxContainer { Name = "GenderChoice" };
        RightContent.AddChild(genders);
        foreach (var gender in Enum.GetValues<RoleGender>())
        {
            var button = RolePreviewControl.ActionButton(gender == RoleGender.Male ? "男" : "女",
                gender == RoleGender.Male ? "GenderMale" : "GenderFemale", () => ChooseGender(gender));
            button.ToggleMode = true;
            button.ButtonGroup = _genders;
            button.ButtonPressed = gender == _gender;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            genders.AddChild(button);
        }
        RightContent.AddChild(_headTitle);
        RightContent.AddChild(_scroll);
        _scroll.AddChild(_heads);
        Resized += ArrangeHeads;
        BuildHeads();
        Preview.DirectionChanged += direction => _directions[(int)_gender] = direction;
        Preview.Actions.AddChild(RolePreviewControl.ActionButton("创建角色", "SubmitCreate", PromptName));
        BackButton.Name = "CancelCreate";
        BackButton.Text = "返回角色列表";
        BackButton.Pressed += () => Cancelled?.Invoke();
        AddChild(_nameDialog);
        var form = new VBoxContainer { CustomMinimumSize = new Vector2(310, 0) };
        _nameDialog.AddChild(form);
        form.AddChild(new Label { Text = "请输入角色名" });
        form.AddChild(_name);
        form.AddChild(_nameFeedback);
        _nameDialog.GetOkButton().Name = "ConfirmCreate";
        _nameDialog.GetCancelButton().Name = "CancelName";
        _nameDialog.Confirmed += Submit;
        _nameDialog.Canceled += RememberName;
        _name.TextSubmitted += _ => Submit();
        Present();
    }

    /// <summary>创建请求期间禁用页面及命名控件，失败保留当前草稿和提示。</summary>
    public void SetBusy(bool busy, string message)
    {
        _busy = busy;
        _name.Editable = !busy;
        foreach (var node in FindChildren("*", "BaseButton", true, false)) ((BaseButton)node).Disabled = busy;
        Feedback.Text = message;
        _nameFeedback.Text = message;
    }

    /// <summary>切换性别时恢复对应头型、朝向和名字，不修改另一性别草稿。</summary>
    private void ChooseGender(RoleGender gender)
    {
        if (_busy || gender == _gender) return;
        RememberName();
        _gender = gender;
        _name.Text = _names[(int)_gender];
        _scroll.ScrollVertical = 0;
        BuildHeads();
        Present();
    }

    /// <summary>每款头型使用独立缩略图按钮平铺，所有条目通过滚动可达。</summary>
    private void BuildHeads()
    {
        foreach (var child in _heads.GetChildren())
        {
            _heads.RemoveChild(child);
            child.QueueFree();
        }
        var group = new ButtonGroup();
        for (var id = AvatarControl.FirstPortraitHeadId; id <= AvatarControl.LastPortraitHeadId; id++)
        {
            var headId = id;
            var button = new Button { Name = $"HeadChoice{id}", ToggleMode = true, ButtonGroup = group,
                ButtonPressed = id == _headIds[(int)_gender], CustomMinimumSize = new Vector2(64, 76),
                TooltipText = $"头型 {id:00}" };
            button.Pressed += () =>
            {
                _headIds[(int)_gender] = headId;
                Present();
            };
            _heads.AddChild(button);
            var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(column);
            column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
            column.AddChild(new TextureRect { Texture = AvatarControl.LoadPortrait(_gender, id),
                CustomMinimumSize = new Vector2(48, 48), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore });
            column.AddChild(new Label { Text = $"{id:00}", HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore });
        }
        ArrangeHeads();
    }

    /// <summary>按页面宽度选择三或四列，避免小窗口出现横向溢出。</summary>
    private void ArrangeHeads() => _heads.Columns = Size.X < 1100 ? 3 : 4;

    /// <summary>恢复当前性别独立的外观和方向。</summary>
    private void Present()
    {
        var count = AvatarControl.LastPortraitHeadId - AvatarControl.FirstPortraitHeadId + 1;
        _headTitle.Text = $"头型 · {_headIds[(int)_gender]:00}（{count} 款可选）";
        Preview.ShowRole(_gender, _headIds[(int)_gender], 0, "角色预览", _directions[(int)_gender]);
    }

    /// <summary>先提示命名，打开窗口本身不发送创建请求。</summary>
    private void PromptName()
    {
        if (_busy) return;
        _name.Text = _names[(int)_gender];
        _nameFeedback.Text = "";
        _nameDialog.ResetSize();
        _nameDialog.PopupCentered(new Vector2I(360, 190));
        _name.GrabFocus();
    }

    /// <summary>确认时记录当前名字草稿，等待 MainController 提交网络请求。</summary>
    private void Submit()
    {
        if (_busy) return;
        RememberName();
        Submitted?.Invoke(_name.Text, _gender, _headIds[(int)_gender]);
    }

    /// <summary>取消、切换或提交时保留当前性别的名字草稿。</summary>
    private void RememberName() => _names[(int)_gender] = _name.Text;
}
