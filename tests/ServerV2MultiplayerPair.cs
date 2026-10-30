using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Godot;

namespace Rpg.Tests;

/// <summary>常规回归启动两个独立Godot进程，使用真实TCP并隔离所有存档。</summary>
public partial class ServerV2MultiplayerPair : Node
{
    /// <summary>隐藏子进程窗口、收集结果并保证失败时回收。</summary>
    public override async void _Ready()
    {
        var root = Path.Combine(Path.GetTempPath(), "rpg-two-process-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        Process? host = null; Process? client = null;
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            Process Start(bool hosting)
            {
                var info = new ProcessStartInfo(OS.GetExecutablePath()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "res://tests/server_v2_multiplayer_smoke.tscn", "--", "--root", root, port.ToString(), hosting ? "--host" : "--client" }) info.ArgumentList.Add(arg);
                return Process.Start(info)!;
            }
            host = Start(true); client = Start(false);
            var hostOut = host.StandardOutput.ReadToEndAsync(); var hostError = host.StandardError.ReadToEndAsync();
            var clientOut = client.StandardOutput.ReadToEndAsync(); var clientError = client.StandardError.ReadToEndAsync();
            await Task.WhenAll(host.WaitForExitAsync(), client.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(80));
            GD.Print(await hostOut); GD.Print(await clientOut);
            if (host.ExitCode != 0 || client.ExitCode != 0 || !File.Exists(Path.Combine(root, "host-done")) || !File.Exists(Path.Combine(root, "client-done")))
                throw new InvalidOperationException(await hostError + await clientError);
            GD.Print("Two Godot TCP processes passed: mutual visibility, movement, role-selection removal and reentry with live TCP."); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            foreach (var process in new[] { host, client }) { if (process is { HasExited: false }) process.Kill(true); process?.Dispose(); }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
