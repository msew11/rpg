using System;
using Godot;
using Rpg.Net.Message;
using Rpg.Services;

namespace Rpg.UI;

/// <summary>开发期聊天输入区，仅将斜杠命令提交给 GM，不实现普通聊天。</summary>
public partial class ChatControl : PanelContainer
{
    /// <summary>只显示普通文本的执行记录。</summary>
    private RichTextLabel _results = null!;
    /// <summary>提交命令的单行输入框。</summary>
    private LineEdit _input = null!;
    /// <summary>当前界面拥有的 GM 结果订阅。</summary>
    private IDisposable? _subscription;

    /// <summary>建立滚动反馈和输入，监听当前角色 GM 执行结果。</summary>
    public override void _Ready()
    {
        Name = "Chat";
        MouseFilter = MouseFilterEnum.Stop;
        var column = new VBoxContainer();
        AddChild(column);
        _results = new RichTextLabel
        {
            Name = "ChatResults", BbcodeEnabled = false, ScrollFollowing = true,
            SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 60),
            MouseFilter = MouseFilterEnum.Stop
        };
        column.AddChild(_results);
        var row = new HBoxContainer();
        column.AddChild(row);
        _input = new LineEdit
        {
            Name = "CommandInput", PlaceholderText = "/item <道具ID> [数量]",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 32)
        };
        row.AddChild(_input);
        _input.TextSubmitted += _ => Submit();
        var send = new Button { Name = "SendCommand", Text = "发送" };
        send.Pressed += Submit;
        row.AddChild(send);
        _subscription = GameServices.EventBus.Subscribe<GmRt>(OnGmResult);
        Append("输入 /item <道具ID> [数量] 发放道具");
        _Process(0);
    }

    /// <summary>聊天底边与默认底栏齐平，空间不足时收窄以避让相邻技能栏。</summary>
    public override void _Process(double delta)
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Min(400, Mathf.Max(160, viewport.X - 32));
        var height = Mathf.Min(180, Mathf.Max(100, viewport.Y - 150));
        var hotbar = GetParent().GetNodeOrNull<Control>("hotbar1");
        if (hotbar is not null && hotbar.GetRect().End.Y > viewport.Y - 16 - height)
            width = Mathf.Min(width, Mathf.Max(160, hotbar.Position.X - 24));
        Size = new Vector2(width, height);
        Position = new Vector2(16, Mathf.Max(16, viewport.Y - Size.Y - 16));
    }

    /// <summary>清理输入后提交 GM，普通文字仅提示本地未开放聊天。</summary>
    private void Submit()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        _input.Clear();
        if (!text.StartsWith('/')) { Append("聊天暂未开放"); return; }
        Append("> " + text);
        try { GameServices.Session.SendGm(text[1..]); }
        catch (Exception error) { Append("失败：" + error.Message); }
    }

    /// <summary>只显示本角色响应，不将服务端文字作为富文本解释。</summary>
    private void OnGmResult(GmRt response)
    {
        if (response.RoleId == GameServices.Session.RoleId)
            Append((response.Success ? "成功：" : "失败：") + response.Message);
    }

    /// <summary>追加普通文字并跟随滚动到底部。</summary>
    private void Append(string text) => _results.AddText(text + "\n");

    /// <summary>HUD 立即退出时解除监听，避免旧界面接收新角色结果。</summary>
    public void ReleaseSubscription()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>离树时幂等释放当前界面的结果订阅。</summary>
    public override void _ExitTree() => ReleaseSubscription();
}
