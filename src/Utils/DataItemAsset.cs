namespace Rpg.Utils;

/// <summary>记录一张道具或武器首帧的来源与无损图集位置。</summary>
public sealed class DataItemAsset
{
    /// <summary>目录内唯一标识，包含来源分类路径。</summary>
    public string Id { get; init; } = "";
    /// <summary>原始文件编号，不代表正式道具名称。</summary>
    public string Name { get; init; } = "";
    /// <summary>由来源目录确定的显示分类。</summary>
    public string Category { get; init; } = "";
    /// <summary>素材库中的 PNG 相对路径。</summary>
    public string Source { get; init; } = "";
    /// <summary>原文件的 SHA256 校验值。</summary>
    public string Sha256 { get; init; } = "";
    /// <summary>裁剪前原图宽度。</summary>
    public int Width { get; init; }
    /// <summary>裁剪前原图高度。</summary>
    public int Height { get; init; }
    /// <summary>透明裁剪区域在原图中的横向偏移。</summary>
    public int CropX { get; init; }
    /// <summary>透明裁剪区域在原图中的纵向偏移。</summary>
    public int CropY { get; init; }
    /// <summary>项目内图集资源路径。</summary>
    public string Atlas { get; init; } = "";
    /// <summary>图集区域左侧像素坐标。</summary>
    public int X { get; init; }
    /// <summary>图集区域顶部像素坐标。</summary>
    public int Y { get; init; }
    /// <summary>裁剪后图像宽度。</summary>
    public int W { get; init; }
    /// <summary>裁剪后图像高度。</summary>
    public int H { get; init; }
}
