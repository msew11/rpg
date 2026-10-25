using Godot;
using Rpg.Items;

namespace ClientTests;

/// <summary>验证 ground-item-pickup 的 alpha 外轮廓，透明角落和内部不能生成矩形描边。</summary>
[Trait("Runtime", "Godot")]
public sealed class ItemOutlineTests
{
    /// <summary>不规则纹理在显示尺寸上形成外轮廓，实体内部与远处角落均透明。</summary>
    [Fact]
    public void OutlineFollowsAlphaRatherThanRectangle()
    {
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        image.FillRect(new Rect2I(12, 4, 5, 20), Colors.White);
        image.FillRect(new Rect2I(12, 19, 15, 5), Colors.White);
        var resource = new DataItemOutline(ImageTexture.CreateFromImage(image));
        Assert.True(resource.IsOpaque(14, 10));
        Assert.False(resource.IsOpaque(2, 2));
        var outline = resource.Outline.GetImage();
        Assert.Equal(0f, outline.GetPixel(16, 12).A);
        Assert.True(outline.GetPixel(13, 12).A > 0.9f);
        Assert.Equal(0f, outline.GetPixel(0, 0).A);
        Assert.Equal(0f, outline.GetPixel(32, 32).A);
    }

    /// <summary>无图的问号占位也有一致的图标与轮廓遮罩。</summary>
    [Fact]
    public void MissingIconUsesQuestionMarkMask()
    {
        var resource = new DataItemOutline(null);
        Assert.False(resource.IsOpaque(0, 0));
        Assert.True(resource.IsOpaque(14, 23));
        Assert.True(resource.Outline.GetImage().GetPixel(15, 25).A > 0);
    }
}
