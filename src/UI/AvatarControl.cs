using System;
using Godot;
using Rpg.Const;
using Rpg.Services;

namespace Rpg.UI;

/// <summary>绘制当前角色头像及左下角等级徽标。</summary>
public partial class AvatarControl : Control
{
    /// <summary>参考资源表中首个有配套 DT 头像的头型编号。</summary>
    public const int FirstPortraitHeadId = 9;

    /// <summary>参考资源表中最后一个有配套 DT 头像的头型编号。</summary>
    public const int LastPortraitHeadId = 72;

    /// <summary>与角色头型对应的头像图。</summary>
    public Texture2D? Portrait { get; set; }

    /// <summary>预览时隐藏只属于游戏 HUD 的等级徽标。</summary>
    public bool ShowLevel
    {
        get => _showLevel;
        set
        {
            _showLevel = value;
            _levelLabel.Visible = value;
            QueueRedraw();
        }
    }

    /// <summary>是否绘制等级徽标。</summary>
    private bool _showLevel = true;

    /// <summary>等级徽标中的固定位置文字。</summary>
    private readonly Label _levelLabel = new() { Name = "LevelLabel", MouseFilter = MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        Position = new Vector2(1, 48), CustomMinimumSize = new Vector2(24, 24) };

    /// <summary>角色当前等级。</summary>
    public int Level
    {
        get => _level;
        set
        {
            _level = value;
            _levelLabel.Text = value.ToString();
            _levelLabel.AddThemeFontSizeOverride("font_size", value >= 100 ? 10 : value >= 10 ? 12 : 14);
        }
    }

    /// <summary>徽标正在显示的等级。</summary>
    private int _level;

    /// <summary>把等级文字固定在头像左下的圆形徽标内。</summary>
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        AddChild(_levelLabel);
        Level = _level;
    }

    /// <summary>在固定头像区域绘制图片、边框和等级。</summary>
    public override void _Draw()
    {
        var center = new Vector2(36, 36);
        DrawCircle(center, 35, new Color("182630"));
        if (Portrait is not null)
            DrawTextureRect(Portrait, new Rect2(4, 4, 64, 64), false);
        DrawArc(center, 34, 0, Mathf.Tau, 64, new Color("87a8b1"), 2, true);
        if (ShowLevel)
        {
            DrawCircle(new Vector2(13, 60), 12, new Color("192a34"));
            DrawArc(new Vector2(13, 60), 12, 0, Mathf.Tau, 32, new Color("87a8b1"), 1.5f, true);
        }
    }

    /// <summary>按性别和头型读取游戏 HUD 与角色预览共用的头像。</summary>
    internal static Texture2D LoadPortrait(RoleGender gender, int headId)
    {
        var id = headId == 0 ? 1 : headId;
        if (id is >= FirstPortraitHeadId and <= LastPortraitHeadId)
        {
            var group = (id - FirstPortraitHeadId) / 8 + 1;
            var variant = (id - FirstPortraitHeadId) % 8 + 1;
            var suffix = gender == RoleGender.Male ? "01" : "51";
            var path = $"res://assets/portraits/DThed0{group}{variant}_{suffix}.png";
            return GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"头像资源加载失败：{path}");
        }
        return GameServices.Animation.GetHead(gender, id).Frames[4];
    }
}
