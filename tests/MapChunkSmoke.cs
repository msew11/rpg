using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Config.Data;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/client-config-map 的分块复用、视口更新和地图边缘场景。</summary>
public partial class MapChunkSmoke : Node
{
    /// <summary>在真实 Godot 视口内执行地图分块验收。</summary>
    public override async void _Ready()
    {
        try
        {
            var viewport = new SubViewport { Size = new Vector2I(1280, 720), Disable3D = true };
            AddChild(viewport);
            var map = new MapManager(new MapConfig
            {
                Kind = "map", Id = "test", Width = 1000, Height = 1000,
                TileSize = 32, DefaultTile = "ground"
            });
            var view = new MapView { Map = map };
            viewport.AddChild(view);
            var camera = new Camera2D { Position = IsometricProjection.ToScreen(new Vector2(16016, 16016)) };
            viewport.AddChild(camera);
            camera.MakeCurrent();
            await Frames(3);

            var initial = Snapshot(view);
            Check(initial.Count is > 0 and < 200, "没有只保留视口附近的分块");
            var draws = 0;
            foreach (var chunk in initial.Values)
                chunk.Connect(CanvasItem.SignalName.Draw, Callable.From(() => draws++));
            await Frames(3);
            Check(draws == 0, "静止时重复绘制了分块");
            Check(SameNodes(initial, Snapshot(view)), "静止时替换了分块");

            camera.Position += new Vector2(256, 0);
            camera.ForceUpdateScroll();
            await Frames(3);
            var moved = Snapshot(view);
            Check(moved.Keys.Except(initial.Keys).Any(), "相机跨界后没有新增分块");
            Check(initial.Keys.Except(moved.Keys).Any(), "相机跨界后没有释放离开的分块");
            Check(initial.Keys.Intersect(moved.Keys).Any(key => ReferenceEquals(initial[key], moved[key])),
                "相机跨界后没有复用仍可见的分块");

            viewport.Size = new Vector2I(800, 600);
            await Frames(3);
            var resized = Snapshot(view);
            Check(resized.Count < moved.Count, "视口缩小时没有缩减分块范围");

            camera.Position = IsometricProjection.ToScreen(Vector2.Zero);
            camera.ForceUpdateScroll();
            await Frames(3);
            var near = Snapshot(view);
            Check(near.ContainsKey("Chunk_0_0"), "地图起点缺少可见分块");
            Check(near.Keys.All(IsValidChunkName), "地图起点创建了边界外分块");

            camera.Position = IsometricProjection.ToScreen(new Vector2(31999, 31999));
            camera.ForceUpdateScroll();
            await Frames(3);
            var far = Snapshot(view);
            Check(far.ContainsKey("Chunk_124_124"), "地图终点缺少可见分块");
            Check(far.Keys.All(IsValidChunkName), "地图终点创建了边界外分块");
            GD.Print("Map chunk smoke passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    /// <summary>等待画布与摄像机完成帧更新。</summary>
    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>记录当前活动分块及其节点身份。</summary>
    private static Dictionary<string, Node2D> Snapshot(MapView view) => view.GetChildren()
        .OfType<Node2D>().ToDictionary(chunk => chunk.Name.ToString());

    /// <summary>检查两个视口状态是否复用同一批节点。</summary>
    private static bool SameNodes(Dictionary<string, Node2D> first, Dictionary<string, Node2D> second) =>
        first.Count == second.Count && first.All(entry => second.TryGetValue(entry.Key, out var node) &&
            ReferenceEquals(entry.Value, node));

    /// <summary>验证分块坐标仍处于千格地图边界内。</summary>
    private static bool IsValidChunkName(string name)
    {
        var parts = name.Split('_');
        return parts.Length == 3 && int.TryParse(parts[1], out var x) && int.TryParse(parts[2], out var y) &&
            x is >= 0 and <= 124 && y is >= 0 and <= 124;
    }

    /// <summary>让验收失败以非零进程码报告。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
