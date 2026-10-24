using System;
using System.Globalization;
using System.Linq;
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
    /// <summary>覆盖于输入末尾且不进入命令文本的数量提示。</summary>
    private Label _quantityHint = null!;
    /// <summary>独立于聊天容器布局的候选浮层。</summary>
    private PanelContainer _suggestions = null!;
    /// <summary>保留输入焦点的可滚动候选列表。</summary>
    private ItemList _candidates = null!;
    /// <summary>Escape 关闭后，在下一次输入前抑制候选。</summary>
    private bool _dismissed;
    /// <summary>当前输入是否位于有效道具后的数量位置。</summary>
    private bool _expectsQuantity;
    /// <summary>格式和道具校验通过时使用的整行文字颜色。</summary>
    private static readonly Color ValidCommandColor = new(0.35f, 0.9f, 0.45f);
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
            Name = "CommandInput",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 32)
        };
        row.AddChild(_input);
        _quantityHint = new Label
        {
            Name = "QuantityHint", Text = "数量", Visible = false,
            MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center
        };
        _quantityHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        _input.AddChild(_quantityHint);
        _suggestions = new PanelContainer
        {
            Name = "CommandSuggestions", TopLevel = true, Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(_suggestions);
        _candidates = new ItemList
        {
            Name = "CommandCandidates", FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop, AllowReselect = true
        };
        _suggestions.AddChild(_candidates);
        _candidates.ItemClicked += (index, _, button) =>
        {
            if (button == (long)MouseButton.Left) CompleteCandidate((int)index);
        };
        _input.TextChanged += _ => { _dismissed = false; RefreshInput(); };
        _input.FocusEntered += RefreshInput;
        _input.FocusExited += () => { _suggestions.Hide(); _quantityHint.Hide(); };
        _input.GuiInput += OnInputKey;
        _input.ItemRectChanged += LayoutHints;
        _input.TextSubmitted += _ => Submit();
        var send = new Button { Name = "SendCommand", Text = "发送" };
        send.Pressed += Submit;
        row.AddChild(send);
        _subscription = GameServices.EventBus.Subscribe<GmRt>(OnGmResult);
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
        LayoutHints();
    }

    /// <summary>按输入阶段重建候选，校验仅表示格式合法，不预判服务端容量或落盘。</summary>
    private void RefreshInput()
    {
        var text = _input.Text.TrimStart();
        var args = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var trailingSpace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        var validItem = args.Length >= 2 && args[0] == "/item" &&
            IsPositiveInt(args[1]) && GameServices.Config.Item.Items.ContainsKey(int.Parse(args[1], CultureInfo.InvariantCulture));
        _expectsQuantity = validItem && args.Length == 2 && trailingSpace;
        if (validItem && args.Length == 3 && IsPositiveInt(args[2]))
            _input.AddThemeColorOverride("font_color", ValidCommandColor);
        else
            _input.RemoveThemeColorOverride("font_color");

        _candidates.Clear();
        if (_input.HasFocus() && !_dismissed && text.StartsWith('/'))
        {
            if (args.Length == 1 && !trailingSpace && "/item".StartsWith(text, StringComparison.Ordinal))
                AddCandidate("/item  —  发放道具", "/item ");
            else if (args.Length >= 1 && args[0] == "/item" &&
                ((args.Length == 1 && trailingSpace) || (args.Length == 2 && !trailingSpace)))
            {
                var prefix = args.Length == 2 ? args[1] : "";
                foreach (var item in GameServices.Config.Item.Items.Values.OrderBy(item => item.Id))
                {
                    var id = item.Id.ToString(CultureInfo.InvariantCulture);
                    if (id.StartsWith(prefix, StringComparison.Ordinal))
                        AddCandidate($"{id}  {item.Name}", $"/item {id} ");
                }
            }
        }
        _suggestions.Visible = _candidates.ItemCount > 0;
        if (_suggestions.Visible) _candidates.Select(0);
        LayoutHints();
    }

    /// <summary>与服务端一致地拒绝符号、非正数和 Int32 溢出。</summary>
    private static bool IsPositiveInt(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;

    /// <summary>将可见说明与实际补全文本分别存入候选。</summary>
    private void AddCandidate(string label, string completion)
    {
        var index = _candidates.AddItem(label);
        _candidates.SetItemMetadata(index, completion);
    }

    /// <summary>补全只修改输入，保留焦点和数量输入位置，不发送请求。</summary>
    private void CompleteCandidate(int index)
    {
        _input.Text = _candidates.GetItemMetadata(index).AsString();
        _input.CaretColumn = _input.Text.Length;
        _input.GrabFocus();
        _dismissed = false;
        RefreshInput();
    }

    /// <summary>候选使用上下键选择、Tab 补全、Escape 隐藏，回车沿用发送行为。</summary>
    private void OnInputKey(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true } key || _input.HasImeText()) return;
        if (!_suggestions.Visible) return;
        switch (key.Keycode)
        {
            case Key.Escape:
                _dismissed = true;
                _suggestions.Hide();
                break;
            case Key.Tab:
                CompleteCandidate(_candidates.GetSelectedItems().FirstOrDefault());
                break;
            case Key.Up:
            case Key.Down:
                var selected = _candidates.GetSelectedItems().FirstOrDefault();
                _candidates.Select(Math.Clamp(selected + (key.Keycode == Key.Up ? -1 : 1), 0, _candidates.ItemCount - 1));
                _candidates.EnsureCurrentIsVisible();
                break;
            default: return;
        }
        AcceptEvent();
    }

    /// <summary>将候选限制在输入上方的视口区域，灰字跟随实际文字且不参与鼠标命中。</summary>
    private void LayoutHints()
    {
        var viewport = GetViewportRect();
        var inputRect = _input.GetGlobalRect();
        if (_suggestions.Visible)
        {
            var width = Mathf.Min(Size.X, viewport.Size.X - 16);
            var height = Mathf.Min(200, Mathf.Min(_candidates.ItemCount * 28 + 12, inputRect.Position.Y - 12));
            _suggestions.Size = new Vector2(width, Mathf.Max(0, height));
            _suggestions.GlobalPosition = new Vector2(
                Mathf.Clamp(inputRect.Position.X, 8, viewport.End.X - width - 8), inputRect.Position.Y - height - 4);
        }
        var font = _input.GetThemeFont("font");
        var fontSize = _input.GetThemeFontSize("font_size");
        var style = _input.GetThemeStylebox("normal");
        var textWidth = font.GetStringSize(_input.Text, fontSize: fontSize).X;
        var x = style.GetContentMargin(Side.Left) + textWidth;
        _quantityHint.Position = new Vector2(x, 0);
        _quantityHint.Size = new Vector2(font.GetStringSize(_quantityHint.Text, fontSize: fontSize).X, _input.Size.Y);
        _quantityHint.Visible = _expectsQuantity && _input.HasFocus() && !_input.HasImeText() &&
            _input.CaretColumn == _input.Text.Length && Mathf.IsZeroApprox(_input.GetScrollOffset()) &&
            x + _quantityHint.Size.X <= _input.Size.X - style.GetContentMargin(Side.Right);
    }

    /// <summary>清理输入后提交 GM，普通文字仅提示本地未开放聊天。</summary>
    private void Submit()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        _input.Clear();
        RefreshInput();
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
