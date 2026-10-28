using System.Linq;
using Rpg.Session.Data;
using System;
using Godot;
using Rpg.Const;
using Rpg.Services;

namespace Rpg.UI;

/// <summary>右侧选择角色并在中央预览，显式确认进入后才建立角色会话。</summary>
public partial class RoleSelectionControl : RoleScreenControl
{
    /// <summary>请求进入中央正在预览的角色。</summary>
    public event Action<long>? EnterRequested;
    /// <summary>打开创建角色页面。</summary>
    public event Action? CreateRequested;
    /// <summary>请求对指定角色显示删除确认。</summary>
    public event Action<long, string>? DeleteRequested;
    /// <summary>从右下角返回登录页。</summary>
    public event Action? LoginRequested;
    /// <summary>当前预览角色标识，与正式会话选择分离。</summary>
    private long _selectedId;
    /// <summary>选角列表仅允许一个条目被选中。</summary>
    private readonly ButtonGroup _selection = new();

    /// <summary>从当前只读摘要建立右侧列表，第一名角色默认预览。</summary>
    public override void _Ready()
    {
        base._Ready();
        RightContent.AddChild(new Label { Text = "选择角色" });
        var scroll = new ScrollContainer { Name = "RoleList", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        RightContent.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        foreach (var role in GameServices.Session.Roles)
        {
            var row = new HBoxContainer();
            list.AddChild(row);
            var button = RolePreviewControl.ActionButton($"{role.Name}\nLv.{role.Level} · {(role.Gender == RoleGender.Male ? "男" : "女")}",
                $"SelectRole{role.RoleId}", () => SelectRole(role.RoleId));
            button.ToggleMode = true;
            button.ButtonGroup = _selection;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(button);
            row.AddChild(RolePreviewControl.ActionButton("删除", $"DeleteRole{role.RoleId}",
                () => DeleteRequested?.Invoke(role.RoleId, role.Name)));
            if (_selectedId == 0)
            {
                button.ButtonPressed = true;
                SelectRole(role.RoleId);
            }
        }
        if (_selectedId == 0)
        {
            list.AddChild(new Label { Text = "暂无角色，请先创建" });
            Preview.ClearRole();
        }
        RightContent.AddChild(RolePreviewControl.ActionButton("创建角色", "OpenCreateRole", () => CreateRequested?.Invoke()));
        var enter = RolePreviewControl.ActionButton("进入游戏", "EnterRole", () =>
        {
            if (_selectedId != 0) EnterRequested?.Invoke(_selectedId);
        });
        enter.Disabled = _selectedId == 0;
        Preview.Actions.AddChild(enter);
        BackButton.Name = "ReturnLogin";
        BackButton.Text = "返回登录";
        BackButton.Pressed += () => LoginRequested?.Invoke();
    }

    /// <summary>点击列表只更新预览，不发送网络请求，不初始化世界。</summary>
    private void SelectRole(long roleId)
    {
        foreach (var role in GameServices.Session.Roles)
        {
            if (role.RoleId != roleId) continue;
            _selectedId = roleId;
            var equipment = role.EquippedItems.Select(id => EquipmentDisplayData.FromItemId(id, GameServices.Config.Item.Items)).ToArray();
            Preview.ShowRole(role.Gender, role.HeadId, role.RoleId, role.Name,
                mainHandId: equipment.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand)?.ItemId ?? 0,
                offHandId: equipment.FirstOrDefault(item => item.Slot == EquipmentSlot.OffHand)?.ItemId ?? 0);
            return;
        }
    }
}
