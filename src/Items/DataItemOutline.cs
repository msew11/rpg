using Godot;

namespace Rpg.Items;

/// <summary>物品表现资源：显示尺寸的 alpha 遮罩、图标与外轮廓，不包含业务对象状态。</summary>
internal sealed class DataItemOutline
{
    /// <summary>图标显示与像素命中共用的 32 像素遮罩。</summary>
    internal Image Mask { get; }
    /// <summary>精确匹配遮罩的图标纹理。</summary>
    internal ImageTexture Icon { get; }
    /// <summary>向外扩展两像素的透明轮廓纹理。</summary>
    internal ImageTexture Outline { get; }

    /// <summary>一次性按显示尺寸生成资源，透明区域保留，不使用矩形边框。</summary>
    internal DataItemOutline(Texture2D? texture)
    {
        Mask = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        if (texture is null)
        {
            // 缺图仍使用问号占位，图形和命中遮罩一致。
            string[] rows = ["01110", "11011", "00011", "00110", "00100", "00000", "00100"];
            for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '1') Mask.FillRect(new Rect2I(8 + x * 3, 5 + y * 3, 3, 3), Colors.White);
        }
        else
        {
            var image = texture.GetImage();
            if (image.IsCompressed()) image.Decompress();
            image.Convert(Image.Format.Rgba8);
            var scale = 32f / Mathf.Max(image.GetWidth(), image.GetHeight());
            var width = Mathf.Max(1, Mathf.RoundToInt(image.GetWidth() * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(image.GetHeight() * scale));
            image.Resize(width, height, Image.Interpolation.Nearest);
            Mask.BlitRect(image, new Rect2I(0, 0, width, height), new Vector2I((32 - width) / 2, (32 - height) / 2));
        }
        Icon = ImageTexture.CreateFromImage(Mask);
        var outline = Image.CreateEmpty(36, 36, false, Image.Format.Rgba8);
        for (var y = -2; y < 34; y++)
        for (var x = -2; x < 34; x++)
        {
            if (IsOpaque(x, y)) continue;
            var neighbor = false;
            for (var dy = -2; dy <= 2 && !neighbor; dy++)
            for (var dx = -2; dx <= 2; dx++)
                if (IsOpaque(x + dx, y + dy)) { neighbor = true; break; }
            if (neighbor) outline.SetPixel(x + 2, y + 2, new Color(1f, 0.88f, 0.25f));
        }
        Outline = ImageTexture.CreateFromImage(outline);
    }

    /// <summary>像素透明角落不参与物品命中。</summary>
    internal bool IsOpaque(int x, int y) => x >= 0 && x < 32 && y >= 0 && y < 32 && Mask.GetPixel(x, y).A > 0.1f;
}
