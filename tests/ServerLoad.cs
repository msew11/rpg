using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Google.Protobuf;
using Rpg.Config;
using Rpg.Net;
using Rpg.Protocol;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Role.Cache;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Environment = System.Environment;
using Codec = Rpg.Server.Protocol.MessageCodec;

namespace Rpg.Tests;

/// <summary>server-movement 独立真实TCP容量验收；不进入常规套件，不使用玩家数据。</summary>
public partial class ServerLoad : Node
{
    /// <summary>解析参数并输出可复核JSON，失败以非零退出。</summary>
    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            string Arg(string key, string fallback) { var index = Array.IndexOf(args, key); return index < 0 ? fallback : args[index + 1]; }
            var count = int.Parse(Arg("--count", "1000")); var dense = args.Contains("--dense");
            var result = await Run(count, dense, int.Parse(Arg("--warmup", "10")), int.Parse(Arg("--seconds", "60")));
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var output = Arg("--out", Path.Combine(Path.GetTempPath(), "rpg-load.json")); File.WriteAllText(output, json);
            GD.Print(json); GetTree().Quit(result.Passed ? 0 : 1);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    /// <summary>准备独立三表和摘要，预热后按时间窗口统计。</summary>
    private static async Task<DataLoadResult> Run(int count, bool dense, int warmup, int seconds)
    {
        var root = Path.Combine(Path.GetTempPath(), "rpg-tcp-load-" + Guid.NewGuid());
        var clients = new List<LoadClient>();
        try
        {
            using var env = new EnvService(Path.Combine(root, "settings"), Path.Combine(root, "server")); using var config = new ConfigService(env);
            env.ServerFlushIntervalSeconds = 0;
            var directory = Path.GetFullPath(env.ServerDirectory); var repo = new RepoRole(directory); var summaries = ImmutableArray.CreateBuilder<DataRoleSummary>();
            var origins = new List<(float X, float Y)>(); var map = config.Map.Items[config.Basic.Value.InitialMapId];
            for (var i = 0; i < count; i++)
            {
                var group = dense ? 0 : i / 20; var x = 1024 + (group % 8) * 4096; var y = 1024 + (group / 8) * 4096;
                origins.Add((x, y)); var id = 1003 + i;
                summaries.Add(new(id, "压测" + i, 1, Server.Modules.Common.Data.RoleGender.Male, 1, []));
                var role = DataCacheRole.CreateInitial(new RoleSummaryPb { RoleId = id, Name = "压测" + i, Level = 1, Gender = RoleGenderPb.Male, HeadId = 1 }, map) with { X = x, Y = y };
                await repo.SaveAsync(id, new(role, [], []), DataRoleTables.All);
            }
            await new RepoAccountRoleSummary(directory).SaveAsync("demo1", summaries.ToImmutable());
            var port = FreePort(); await using var server = new MockServer(env, config);
            var errors = new ConcurrentQueue<string>(); server.Error += error => errors.Enqueue(error.ToString()); server.Start(port);
            for (var i = 0; i < count; i++)
            {
                var client = new LoadClient(server); clients.Add(client); await client.Connect(port);
                await client.Request(new Login { Username = "demo1", Password = "123" }); await client.Request(new EnterWorld { RoleId = 1003 + i });
            }
            async Task MoveFor(int duration)
            {
                var clock = Stopwatch.StartNew(); var round = 0;
                while (clock.Elapsed.TotalSeconds < duration)
                {
                    var flip = round++ / 10 % 2 == 0;
                    await Task.WhenAll(clients.Select((client, i) => client.Move(origins[i].X + (flip ? 400 : 0), origins[i].Y + (flip ? 400 : 0))));
                    await Task.Delay(200);
                }
            }
            await MoveFor(warmup);
            while (server.World!.RoundMilliseconds.TryDequeue(out _)) { }
            foreach (var client in clients) client.BeginMeasure();
            var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var start = Stopwatch.StartNew();
            var gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray(); var queues = new List<long>(); var memory = new List<long>();
            var moving = MoveFor(seconds);
            while (!moving.IsCompleted) { queues.Add(server.QueuedOutputBytes); process.Refresh(); memory.Add(process.WorkingSet64); await Task.Delay(100); }
            await moving; var elapsed = start.Elapsed.TotalSeconds; process.Refresh();
            var latency = clients.SelectMany(client => client.Latencies).ToArray(); var actions = clients.SelectMany(client => client.ActionLatencies).ToArray();
            var rounds = server.World.RoundMilliseconds.ToArray(); var disconnects = clients.Sum(client => client.Disconnects);
            var result = new DataLoadResult(count, dense, warmup, seconds, Environment.OSVersion.ToString(), Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "", Environment.ProcessorCount,
                (process.TotalProcessorTime - cpu).TotalSeconds / elapsed * 100, memory.DefaultIfEmpty().Max(),
                Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - gc[index]).ToArray(), clients.Sum(client => client.BytesRead), clients.Sum(client => client.BytesWritten),
                queues.DefaultIfEmpty().Max(), queues.Take(10).DefaultIfEmpty().Average(), queues.TakeLast(10).DefaultIfEmpty().Average(),
                Percentile(rounds), Percentile(latency), Percentile(actions), clients.Sum(client => client.Frames), clients.Min(client => client.LastVisible), disconnects, errors.ToArray(),
                disconnects == 0 && errors.IsEmpty && rounds.Length > 0 && latency.Length > 0 && Percentile(rounds) <= 50 && Percentile(actions) <= 200 &&
                queues.TakeLast(10).DefaultIfEmpty().Average() < 8 * 1024 * 1024);
            return result;
        }
        finally
        {
            foreach (var client in clients) await client.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    /// <summary>统计P95，空窗口显式失败。</summary>
    private static double Percentile(double[] values) { Array.Sort(values); return values.Length == 0 ? double.MaxValue : values[(int)Math.Ceiling(values.Length * .95) - 1]; }
    /// <summary>临时监听仅用于分配空闲本地端口。</summary>
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    /// <summary>机器信息和全部测量值；CPU包含同进程负载客户端。</summary>
    private sealed record DataLoadResult(int Connections, bool Dense, int WarmupSeconds, int MeasureSeconds, string OS, string CPU, int LogicalProcessors,
        double CombinedCpuPercentOneCore, long PeakWorkingSetBytes, int[] GCCollections, long ReceivedBytes, long SentBytes,
        long PeakQueueBytes, double FirstQueueBytes, double LastQueueBytes, double RoundP95Ms, double FrameAgeP95Ms, double ActionEndToEndP95Ms,
        long Frames, int MinimumVisiblePlayers, int Disconnects, string[] Errors, bool Passed);

    /// <summary>持续读取主动推送的真实socket客户端，避免靠请求轮询世界。</summary>
    private sealed class LoadClient(MockServer server) : IAsyncDisposable
    {
        /// <summary>物理连接和停止信号。</summary>
        private readonly TcpClient _client = new() { NoDelay = true };
        /// <summary>读取循环关闭信号。</summary>
        private readonly CancellationTokenSource _stop = new();
        /// <summary>关联请求等待。</summary>
        private readonly ConcurrentDictionary<ulong, TaskCompletionSource<IMessage>> _pending = new();
        /// <summary>动作首次发出时间。</summary>
        private readonly ConcurrentDictionary<ulong, long> _actions = new();
        /// <summary>后台读取任务。</summary>
        private Task _reader = Task.CompletedTask;
        /// <summary>当前请求与动作编号。</summary>
        private ulong _request, _action, _ack;
        /// <summary>测量窗口标记。</summary>
        private volatile bool _measure;
        /// <summary>样本与吞吐计数。</summary>
        internal ConcurrentQueue<double> Latencies { get; } = new();
        /// <summary>动作到权威帧确认样本。</summary>
        internal ConcurrentQueue<double> ActionLatencies { get; } = new();
        /// <summary>累计接收流量。</summary>
        internal long BytesRead;
        /// <summary>累计发送流量。</summary>
        internal long BytesWritten;
        /// <summary>测量内世界帧数。</summary>
        internal long Frames;
        /// <summary>异常断连数。</summary>
        internal int Disconnects;
        /// <summary>最近可见人数。</summary>
        internal int LastVisible;
        /// <summary>连接后启动唯一读取任务。</summary>
        internal async Task Connect(int port) { await _client.ConnectAsync(IPAddress.Loopback, port); _reader = Read(); }
        /// <summary>发送请求等待真实响应，初始化也经过生产路由。</summary>
        internal async Task<IMessage> Request(IMessage request)
        {
            var id = ++_request; var completion = new TaskCompletionSource<IMessage>(TaskCreationOptions.RunContinuationsAsynchronously); _pending[id] = completion;
            await Write(request, id); var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (result is ErrorRt error) throw new InvalidOperationException(error.Message); return result;
        }
        /// <summary>移动只发送意图，确认通过主动推送统计。</summary>
        internal Task Move(float x, float y) { var action = ++_action; _actions[action] = Stopwatch.GetTimestamp(); return Write(new Move { ActionId = action, Target = new() { X = x, Y = y } }, ++_request); }
        /// <summary>所有调用由单个负载回合串行发出。</summary>
        private async Task Write(IMessage message, ulong id) { var bytes = Codec.Encode(message, 1, id, true); await TcpFraming.WriteAsync(_client.GetStream(), bytes, _stop.Token); if (_measure) Interlocked.Add(ref BytesWritten, bytes.Length + 4); }
        /// <summary>持续读取关联响应和世界帧，统计确认时延与可见范围。</summary>
        private async Task Read()
        {
            try
            {
                while (await TcpFraming.ReadAsync(_client.GetStream(), _stop.Token) is { } bytes)
                {
                    var envelope = Codec.ReadEnvelope(bytes); var message = Codec.ReadPayload(envelope, false);
                    if (_measure) Interlocked.Add(ref BytesRead, bytes.Length + 4);
                    if (_pending.TryRemove(envelope.RequestId, out var completion)) completion.TrySetResult(message);
                    if (message is WorldUpdateNotice notice)
                    {
                        var frame = notice.Frame;
                        if (frame.Snapshot is { } snapshot) LastVisible = snapshot.Roles.Count;
                        else if (frame.Delta is { } delta) LastVisible += delta.EnteredRoles.Count - delta.LeftRoleIds.Count;
                        if (_measure) { Interlocked.Increment(ref Frames); Latencies.Enqueue(server.World!.Now - frame.ServerTimeMs); }
                        if (frame.AcknowledgedAction > _ack)
                        {
                            _ack = frame.AcknowledgedAction;
                            if (_actions.TryGetValue(_ack, out var sent) && _measure) ActionLatencies.Enqueue(Stopwatch.GetElapsedTime(sent).TotalMilliseconds);
                            foreach (var id in _actions.Keys.Where(id => id <= _ack)) _actions.TryRemove(id, out _);
                        }
                    }
                    if (message is SessionKickedNotice or ErrorRt) throw new InvalidOperationException(message.ToString());
                }
                if (!_stop.IsCancellationRequested) Interlocked.Increment(ref Disconnects);
            }
            catch (Exception) when (_stop.IsCancellationRequested) { }
            catch (Exception error) { Interlocked.Increment(ref Disconnects); foreach (var pending in _pending.Values) pending.TrySetException(error); }
        }
        /// <summary>预热结束重置计数。</summary>
        internal void BeginMeasure() { BytesRead = 0; BytesWritten = 0; _measure = true; }
        /// <summary>停止读取并释放连接。</summary>
        public async ValueTask DisposeAsync() { _stop.Cancel(); _client.Dispose(); await _reader; _stop.Dispose(); }
    }
}
