using Godot;
using Rpg.UI;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-ui 的开窗、角色外观和确认取消音效场景。</summary>
[Trait("Runtime", "Godot")]
public sealed class UiSoundTests
{
    /// <summary>界面入口与角色预览操作使用 1003，不落入默认确认音效。</summary>
    [Theory]
    [InlineData("SoundCase", "系统")]
    [InlineData("SoundCase", "角色")]
    [InlineData("SoundCase", "背包")]
    [InlineData("SoundCase", "大地图")]
    [InlineData("SoundCase", "GM")]
    [InlineData("SoundCase", "系统设置")]
    [InlineData("SoundCase", "角色设置")]
    [InlineData("OpenItemCatalog", "查看道具图鉴")]
    [InlineData("SoundCase", "进入 HUD 调整")]
    [InlineData("OpenCreateRole", "创建角色")]
    [InlineData("SubmitCreate", "创建角色")]
    [InlineData("GenderMale", "男")]
    [InlineData("GenderFemale", "女")]
    [InlineData("HeadChoice1", "")]
    [InlineData("HeadChoice74", "")]
    [InlineData("RotateClockwise", "顺时针旋转")]
    [InlineData("RotateCounterclockwise", "逆时针旋转")]
    public void OpeningAndPreviewButtonsUseMenuSound(string name, string label)
    {
        var button = new Button { Name = name, Text = label };
        Assert.Equal(1003, UiSoundPlayer.SoundIdForButton(button));
        button.Free();
    }

    /// <summary>确认、取消和登录入口仍遵循 openspec/specs/client-ui 的原有分类。</summary>
    [Theory]
    [InlineData("ConfirmCreate", "确认创建", 1006)]
    [InlineData("SoundCase", "保存", 1006)]
    [InlineData("ClosePanel", "关闭", 1007)]
    [InlineData("CancelName", "取消", 1007)]
    [InlineData("SoundCase", "登录", 0)]
    public void ConfirmationAndCancellationKeepTheirSound(string name, string label, int expected)
    {
        var button = new Button { Name = name, Text = label };
        Assert.Equal(expected, UiSoundPlayer.SoundIdForButton(button));
        button.Free();
    }
}
