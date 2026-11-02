using System;
using System.Globalization;
using System.Linq;
using Godot;
using Rpg.Protocol;
using Rpg.Services;

namespace Rpg.UI;

/// <summary>开发期聊天输入区，仅将斜杠命令提交给 GM，不实现普通聊天。</summary>
public partial class ChatControl : PanelContainer
{
    /// <summary>命令反馈监听。</summary>
    private readonly System.Collections.Generic.List<IDisposable> _subscriptions = new();
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
    /// <summary>当前候选是否需要回车进入怪物放置。</summary>
    private bool _monsterCandidates;
    /// <summary>确认怪物后交给当前世界建立本地放置预览。</summary>
    public event Action<int>? MonsterPlacementRequested;
    /// <summary>格式和道具校验通过时使用的整行文字颜色。</summary>
    private static readonly Color ValidCommandColor = new(0.35f, 0.9f, 0.45f);

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
            if (button == (long)MouseButton.Left && !_monsterCandidates) CompleteCandidate((int)index);
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

        _subscriptions.Add(GameServices.EventBus.Subscribe<Rpg.Protocol.GmRt>(response =>
        { if (response.RoleId == GameServices.Session.RoleId) Append("成功：" + response.Message); }));
        _subscriptions.Add(GameServices.EventBus.Subscribe<Rpg.Protocol.ErrorRt>(error => Append("失败：" + error.Message)));
        _Process(0);
    }

    /// <summary>随 HUD 位置和窗口变化同步候选浮层。</summary>
    public override void _Process(double delta) => LayoutHints();

    /// <summary>按输入阶段重建候选，校验仅表示格式合法，不预判服务端容量或落盘。</summary>
    private void RefreshInput()
    {
        var text = _input.Text.TrimStart();
        var args = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var trailingSpace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        var validItem = args.Length >= 2 && args[0] == "/item" &&
            IsPositiveInt(args[1]) && GameServices.Config.Item.Items.ContainsKey(int.Parse(args[1], CultureInfo.InvariantCulture));
        _expectsQuantity = validItem && args.Length == 2 && trailingSpace;
        if (validItem && args.Length == 3 && IsPositiveInt(args[2]) ||
            args.Length == 2 && args[0] == "/movespeed" && (args[1] == "off" || double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) && double.IsFinite(speed) && speed >= 0))
            _input.AddThemeColorOverride("font_color", ValidCommandColor);
        else
            _input.RemoveThemeColorOverride("font_color");

        _candidates.Clear();
        _monsterCandidates = false;
        if (_input.HasFocus() && !_dismissed && text.StartsWith('/'))
        {
            if (args.Length is 1 or 2 && args[0] == "/monster")
            {
                _monsterCandidates = true;
                var prefix = args.Length == 2 ? args[1] : "";
                foreach (var monster in GameServices.Config.Monster.Items.Values.OrderBy(value => value.Id))
                    if (monster.Id.ToString(CultureInfo.InvariantCulture).StartsWith(prefix, StringComparison.Ordinal) || monster.Name.Contains(prefix, StringComparison.Ordinal))
                        AddCandidate($"{monster.Id}  {monster.Name}", monster.Id);
            }
            else if (args.Length == 1 && !trailingSpace && "/item".StartsWith(text, StringComparison.Ordinal))
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
        if (_input.HasFocus() && !_dismissed && args.Length == 1 && !trailingSpace && "/movespeed".StartsWith(text, StringComparison.Ordinal))
            AddCandidate("/movespeed <非负数|off>  —  保存当前角色移速", "/movespeed ");
        if (_input.HasFocus() && !_dismissed && args.Length == 1 && !trailingSpace && text != "/monster" && "/monster".StartsWith(text, StringComparison.Ordinal))
            AddCandidate("/monster  —  选择怪物并点击地面放置", "/monster ");
        _suggestions.Visible = _candidates.ItemCount > 0;
        if (_suggestions.Visible) _candidates.Select(0);
        LayoutHints();
    }

    /// <summary>与服务端一致地拒绝符号、非正数和 Int32 溢出。</summary>
    private static bool IsPositiveInt(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;

    /// <summary>将可见说明与补全文本或整型怪物 ID 分别存入候选。</summary>
    private void AddCandidate(string label, Variant completion)
    {
        var index = _candidates.AddItem(label);
        _candidates.SetItemMetadata(index, completion);
    }

    /// <summary>怪物确认进入本地预览，其他候选只补全文本；均不发送请求。</summary>
    private void CompleteCandidate(int index)
    {
        if (_monsterCandidates)
        {
            var id = _candidates.GetItemMetadata(index).AsInt32();
            _input.Clear();
            _dismissed = true;
            _suggestions.Hide();
            _input.ReleaseFocus();
            MonsterPlacementRequested?.Invoke(id);
            return;
        }
        _input.Text = _candidates.GetItemMetadata(index).AsString();
        _input.CaretColumn = _input.Text.Length;
        _input.GrabFocus();
        _dismissed = false;
        RefreshInput();
    }

    /// <summary>回车优先确认候选，无候选时补齐小键盘回车提交；消费已处理的按键。</summary>
    private void OnInputKey(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true } key || _input.HasImeText()) return;
        if (!_suggestions.Visible)
        {
            if (key.Keycode == Key.KpEnter)
            {
                Submit();
                AcceptEvent();
            }
            return;
        }
        switch (key.Keycode)
        {
            case Key.Escape:
                _dismissed = true;
                _suggestions.Hide();
                break;
            case Key.Tab:
            case Key.Enter:
            case Key.KpEnter:
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

    /// <summary>怪物命令只展开本地列表，其他斜杠提交 GM，普通文字提示聊天未开放。</summary>
    private void Submit()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0) return;
        var args = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args[0] == "/monster")
        {
            if (args.Length > 2) { Append("请使用 /monster 选择怪物，再点击地面确认坐标"); return; }
            _input.GrabFocus();
            _dismissed = false;
            RefreshInput();
            return;
        }
        _input.Clear();
        RefreshInput();
        if (!text.StartsWith('/')) { Append("聊天暂未开放"); return; }
        Append("> " + text);
        try { GameServices.Session.SendGm(text[1..]); }
        catch (Exception error) { Append("失败：" + error.Message); }
    }

    /// <summary>追加普通文字并跟随滚动到底部。</summary>
    private void Append(string text) => _results.AddText(text + "\n");

    /// <summary>HUD 立即退出时解除监听，避免旧界面接收新角色结果。</summary>
    public void ReleaseSubscription()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    /// <summary>离树时幂等释放当前界面的结果订阅。</summary>
    public override void _ExitTree() => ReleaseSubscription();
}
