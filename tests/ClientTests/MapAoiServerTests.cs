using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/map-aoi 的九块差集、权威移动、对象可见性及协议边界。</summary>
public sealed class MapAoiServerTests : IDisposable
{
    /// <summary>测试独占的地图/角色目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-aoi-" + Guid.NewGuid());
    /// <summary>只通过真实序列化后收集消息。</summary>
    private readonly List<IMessage> _messages = [];
    /// <summary>两个独立地图的真实 MockServer。</summary>
    private readonly MockServer _server;
    /// <summary>当前选中的角色。</summary>
    private readonly DataServerRole _role;

    /// <summary>将角色放在中心块 (2,2)，使完整九块范围可被验收。</summary>
    public MapAoiServerTests()
    {
        var roles = new RepoServerRole(_directory);
        _role = roles.Create("地图测试", ServerRoleGender.Male, 1) with { X = 800, Y = 800 };
        roles.SaveRole(_role);
        _server = new MockServer(_directory, new(8, 5, []), maps:
            [new(1, 100, 100, 32, 10), new(2, 100, 100, 32, 10)]);
        _server.Outgoing += bytes => _messages.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false));
        _server.ResetSession(1);
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = _role.RoleId });
    }

    /// <summary>所有请求通过已有字节信封。</summary>
    private void Send(IMessage request) => _server.Receive(MessageCodec.Encode(request, 1, true));

    /// <summary>首次九块，水平跨界三进三出，不等待目标到达或重发交集。</summary>
    [Fact]
    public void InitialNineChunksAndMovementDifferenceAreAuthoritative()
    {
        var initial = Assert.Single(_messages.OfType<MapAoiNotice>()).Snapshot;
        Assert.Equal(9, initial.Chunks.Count); Assert.Equal(1, initial.Sequence);
        Assert.All(initial.Chunks, chunk => Assert.Empty(chunk.Objects));
        _messages.Clear(); _server.Tick(0); Assert.Empty(_messages);
        Send(new Move { RoleId = _role.RoleId, ActionId = 1, X = 2000, Y = 800 });
        _server.Tick(0.8);
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        _server.Tick(0.1);
        var notice = Assert.Single(_messages.OfType<MapAoiNotice>());
        Assert.Equal(3, notice.Delta.EnteredChunks.Count);
        Assert.Equal(3, notice.Delta.LeftChunks.Count);
        Assert.Equal(3, notice.Delta.Center.X);
        Assert.Equal(2, notice.Sequence);
    }

    /// <summary>对象由范围内移出、远处更新、移回；其他地图变化完全隔离。</summary>
    [Fact]
    public void ObjectVisibilityChangesWithoutLeakingOtherMaps()
    {
        _messages.Clear();
        var value = _server.CreateMapObject(1, new() { X = 800, Y = 800, MonsterTypeId = "test" });
        Assert.Single(Assert.Single(_messages.OfType<MapAoiNotice>()).Delta.UpsertObjects);
        _messages.Clear();
        value = _server.UpdateMapObject(1, value with { X = 2000 });
        Assert.Equal(value.ObjectId.ToString("N"), Assert.Single(Assert.Single(_messages.OfType<MapAoiNotice>()).Delta.RemovedObjectIds));
        _messages.Clear();
        value = _server.UpdateMapObject(1, value with { X = 2100 });
        _server.CreateMapObject(2, new() { X = 800, Y = 800, MonsterTypeId = "other" });
        Assert.Empty(_messages);
        value = _server.UpdateMapObject(1, value with { X = 1100 });
        var notice = Assert.Single(_messages.OfType<MapAoiNotice>());
        Assert.Equal(4, notice.Sequence); // 只有三次可见变化推进序号。
        Assert.Equal(value.Revision, Assert.Single(notice.Delta.UpsertObjects).Revision);
        _messages.Clear(); _server.RemoveMapObject(1, value.ObjectId);
        Assert.Single(Assert.Single(_messages.OfType<MapAoiNotice>()).Delta.RemovedObjectIds);
    }

    /// <summary>返回登录不删对象，禁用周期后关闭仍落地；其他地图失败不阻止角色和地图提交。</summary>
    [Fact]
    public void ResetKeepsObjectsAndFlushContinuesOtherDomains()
    {
        var one = _server.CreateMapObject(1, new() { X = 100, Y = 100, MonsterTypeId = "one" });
        var two = _server.CreateMapObject(2, new() { X = 100, Y = 100, MonsterTypeId = "two" });
        var blocked = Path.Combine(_directory, "maps", "1");
        Directory.CreateDirectory(Path.GetDirectoryName(blocked)!); File.WriteAllText(blocked, "阻止创建目录");
        Assert.Throws<AggregateException>(() => _server.Flush());
        Assert.True(File.Exists(Path.Combine(_directory, "maps", "2", "manifest.json")));
        Assert.Equal(one, _server.GetMap(1).Objects[one.ObjectId]);
        File.Delete(blocked); _server.ResetSession(2); _server.Flush();
        Assert.Equal(one, Assert.Single(new ServerMap(new(1, 100, 100, 32, 10), new(_directory), new Dictionary<int, int>()).Objects).Value);
        Assert.Equal(two, Assert.Single(_server.GetMap(2).Objects).Value);
    }

    /// <summary>重同步只取当前权威范围，越界移动及错误地图请求不改变状态。</summary>
    [Fact]
    public void ResyncAndBoundsAreValidated()
    {
        _messages.Clear();
        Send(new ResyncMapAoi { RoleId = _role.RoleId, MapId = 2 });
        Assert.False(Assert.Single(_messages.OfType<ResyncMapAoiRt>()).Success);
        _messages.Clear(); Send(new ResyncMapAoi { RoleId = _role.RoleId, MapId = 1 });
        var result = Assert.Single(_messages.OfType<ResyncMapAoiRt>());
        Assert.True(result.Success); Assert.Equal(2, result.Snapshot.Sequence); Assert.Equal(9, result.Snapshot.Chunks.Count);
        _messages.Clear(); Send(new Move { RoleId = _role.RoleId, ActionId = 1, X = 99999, Y = 0 });
        Assert.False(Assert.Single(_messages.OfType<MoveRt>()).Success);
        _messages.Clear();
        _server.PublishRoleState(_role with { X = 3199, Y = 3199 });
        var delta = Assert.Single(_messages.OfType<MapAoiNotice>()).Delta;
        Assert.Equal(4, delta.EnteredChunks.Count);
        Assert.Equal(9, delta.LeftChunks.Count);
    }

    /// <summary>清理隔离目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
