using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/movement-performance：隔离存档测量真实进入、静止和跨分块移动。</summary>
public partial class MovementPerformance : Node
{
    /// <summary>自动启动真实控制器，输出同设置下可比较的帧时间，不以硬件阈值判定失败。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-performance-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            var clock = Stopwatch.StartNew();
            main = new DoubleMainController
            {
                SettingsDirectory = Path.Combine(directory, "settings"),
                ServerDirectory = Path.Combine(directory, "server")
            };
            AddChild(main);
            GameServices.Session.Login("demo", "demo");
            await Until(() => GameServices.Session.IsLoggedIn);
            GameServices.Session.SelectRole(1001);
            await Until(() => GameServices.World.HasRole);
            GD.Print($"ENTER world_ms={clock.Elapsed.TotalMilliseconds:F2} renderer={RenderingServer.GetCurrentRenderingMethod()}");
            await Measure("entry", 60, false);
            await Measure("idle", 240, false);
            var capture = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--capture="));
            if (capture is not null)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                var result = image.SavePng(capture["--capture=".Length..]);
                if (result != Error.Ok) throw new IOException($"截图保存失败：{result}");
            }
            var initial = GameServices.World.RolePosition;
            await Measure("moving", 720, true);
            if (GameServices.World.RolePosition.DistanceTo(initial) < 100)
                throw new InvalidOperationException("连续移动没有改变角色位置");
            await Measure("after_input", 120, false);
            GD.Print("Movement performance passed.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            if (main is not null) { RemoveChild(main); main.Free(); }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>等待真实固定帧的服务响应，并为启动失败设置截止时间。</summary>
    private async Task Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed.TotalSeconds > 15) throw new TimeoutException("登录或选角超时");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>采样帧间隔和分配，并通过正式输入契约改目标；引擎耗时监控约每秒更新，可能包含上一阶段。</summary>
    private async Task Measure(string stage, int count, bool moving)
    {
        var frames = new List<double>(count);
        var cpu = new List<double>(count);
        var physics = new List<double>(count);
        var allocated = GC.GetTotalAllocatedBytes(true);
        var collections = GC.CollectionCount(0);
        var last = Stopwatch.GetTimestamp();
        using var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
        for (var index = 0; index < count; index++)
        {
            if (moving && index % 90 == 0)
                GameServices.World.HandleInput(click, GameServices.World.RolePosition + new Vector2(1200, 800));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Stopwatch.GetTimestamp();
            frames.Add(Stopwatch.GetElapsedTime(last, now).TotalMilliseconds);
            last = now;
            cpu.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
            physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000);
        }
        var bytes = GC.GetTotalAllocatedBytes(true) - allocated;
        frames.Sort(); cpu.Sort(); physics.Sort();
        GD.Print($"PERF {stage} frames={count} p50_ms={frames[count / 2]:F2} p95_ms={frames[(int)(count * .95)]:F2} p99_ms={frames[(int)(count * .99)]:F2} max_ms={frames[^1]:F2} over50={frames.Count(value => value > 50)} process_monitor_max_ms={cpu[^1]:F2} physics_monitor_max_ms={physics[^1]:F2} bytes_per_frame={bytes / count} gen0={GC.CollectionCount(0) - collections}");
    }
}
