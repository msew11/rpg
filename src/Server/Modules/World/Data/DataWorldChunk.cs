namespace Rpg.Server.Modules.World.Data;

/// <summary>不可变存储块坐标，不限制其是否处于地图范围内。</summary>
/// <param name="X">横向块坐标。</param>
/// <param name="Y">纵向块坐标。</param>
public readonly record struct DataWorldChunk(int X, int Y);
