using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 ground-item-pickup 的权威距离、整份入包、取消与反向共同落地。</summary>
public sealed class GroundItemPickupTests : IDisposable
{
    /// <summary>隔离服务端文件目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-pickup-" + Guid.NewGuid());
    /// <summary>真实字节输出的完整消息。</summary>
    private readonly List<IMessage> _messages = [];
    /// <summary>有限容量及可堆叠道具规则。</summary>
    private readonly DataServerBagRules _rules = new(2, 2, [new(2000, "测试堆叠", 10)]);
    /// <summary>文件核对用角色仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>当前角色身份。</summary>
    private readonly long _id;
    /// <summary>真实请求服务端。</summary>
    private MockServer _server;
    /// <summary>本会话递增的移动序号。</summary>
    private long _action;

    /// <summary>初始持有八件堆叠，角色位于地图内部。</summary>
    public GroundItemPickupTests()
    {
        _repo = new(_root);
        var role = _repo.Create("拾取测试", ServerRoleGender.Male, 1, spawnX: 1500, spawnY: 1500);
        _id = role.RoleId;
        _repo.SaveBag(role with { Bag = new() { Revision = 1, NextInstanceId = 2, Items = [new(1, 2000, 0, 8)] } });
        _server = Start();
        Enter(_id);
    }

    /// <summary>重建不调用旧服务器关闭，用于模拟异常退出。</summary>
    private MockServer Start()
    {
        _action = 0;
        var server = new MockServer(_root, _rules, flushInterval: () => 0,
            maps: [new(1, 100, 100, 32, 32), new(2, 100, 100, 32, 32)]);
        server.ResetSession(1);
        server.Outgoing += packet => _messages.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        return server;
    }

    /// <summary>登录并选角，跨角色时保持服务器内存。</summary>
    private void Enter(long id)
    {
        _action = 0;
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = id });
        Assert.True(_messages.OfType<SelectRoleRt>().Last().Success);
        _messages.Clear();
    }

    /// <summary>始终使用正式协议方向及会话信封。</summary>
    private void Send(IMessage message) => _server.Receive(MessageCodec.Encode(message, 1, true));

    /// <summary>创建指定位置的合法地面测试对象。</summary>
    private DataServerMapObject Ground(int count = 5, float x = 1500, int mapId = 1) =>
        _server.CreateMapObject(mapId, new() { X = x, Y = 1500, ItemId = 2000, Count = count });

    /// <summary>对应真实客户端先移动后绑定拾取的顺序。</summary>
    private PickupItem Begin(DataServerMapObject item, long version = 1, long? role = null)
    {
        _messages.Clear();
        var id = role ?? _id;
        Send(new Move { RoleId = id, ActionId = ++_action, X = item.X, Y = item.Y });
        var request = new PickupItem { RoleId = id, ObjectId = item.ObjectId.ToString("N"),
            ExpectedObjectRevision = item.Revision, ExpectedBagRevision = version, MoveActionId = _action };
        Send(request);
        return request;
    }

    /// <summary>先补原堆叠再占空格，完整对象移除，重复请求不会再次增加。</summary>
    [Fact]
    public void PickupFillsStackThenEmptySlotAndRejectsReplay()
    {
        var item = Ground();
        var request = Begin(item);
        Assert.True(Assert.Single(_messages.OfType<PickupItemRt>()).Success);
        var bag = Assert.Single(_messages.OfType<BagChangedNotice>()).Bag;
        Assert.Equal(new[] { 10, 3 }, bag.Items.OrderBy(item => item.SlotIndex).Select(item => item.Count));
        Assert.Equal(new long[] { 1, 2 }, bag.Items.OrderBy(item => item.SlotIndex).Select(item => item.InstanceId));
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.Single(Assert.Single(_messages.OfType<MapAoiNotice>()).Delta.RemovedObjectIds);
        _messages.Clear(); Send(request);
        Assert.False(Assert.Single(_messages.OfType<PickupItemRt>()).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        _server.Flush();
        Assert.Equal(13, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        Assert.Equal(3, _repo.Load(_id).Bag.NextInstanceId);
    }

    /// <summary>移动中间位置达到一个瓦片距离才拾取，同一在途请求重发仍只有一个最终结果。</summary>
    [Fact]
    public void WaitsUntilAuthoritativeDistanceAndCoalescesPendingDuplicate()
    {
        var item = Ground(x: 1600);
        var request = Begin(item);
        Send(request);
        Assert.Empty(_messages.OfType<PickupItemRt>());
        _server.Tick(0.2);
        Assert.Empty(_messages.OfType<PickupItemRt>());
        Assert.Single(_server.GetMap(1).Objects);
        _server.Tick(0.2);
        Assert.True(Assert.Single(_messages.OfType<PickupItemRt>()).Success);
        Assert.Empty(_server.GetMap(1).Objects);
    }

    /// <summary>总容量差一件也整份失败，不先补部分堆叠，不消耗版本与实例编号。</summary>
    [Fact]
    public void FullBagRejectsWholeObjectWithoutPartialMerge()
    {
        var role = _repo.Load(_id);
        _repo.SaveBag(role with { Bag = new() { Revision = 1, NextInstanceId = 5,
            Items = [new(1, 2000, 0, 9), new(2, 2000, 1, 10), new(3, 2000, 2, 10), new(4, 2000, 3, 10)] } });
        _server = Start(); Enter(_id);
        var item = Ground(2);
        Begin(item);
        Assert.Contains("背包空间不足", Assert.Single(_messages.OfType<PickupItemRt>()).Error);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Equal(item, Assert.Single(_server.GetMap(1).Objects).Value);
        _server.Flush();
        Assert.Equal(39, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        Assert.Equal(1, _repo.Load(_id).Bag.Revision);
        Assert.Equal(5, _repo.Load(_id).Bag.NextInstanceId);
    }

    /// <summary>错误身份、版本、地图或非物品对象均不能绕过校验。</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("bag")]
    [InlineData("object")]
    [InlineData("map")]
    [InlineData("far")]
    [InlineData("monster")]
    [InlineData("move")]
    public void InvalidRequestsKeepBothSides(string kind)
    {
        var item = kind == "monster" ? _server.CreateMapObject(1, new() { X = 1500, Y = 1500, MonsterTypeId = "test" })
            : Ground(x: kind == "far" ? 3199 : 1500, mapId: kind == "map" ? 2 : 1);
        Send(new Move { RoleId = _id, ActionId = ++_action, X = kind == "far" ? 1500 : item.X, Y = item.Y });
        _messages.Clear();
        Send(new PickupItem { RoleId = kind == "role" ? _id + 1 : _id, ObjectId = item.ObjectId.ToString("N"),
            ExpectedBagRevision = kind == "bag" ? 0 : 1, ExpectedObjectRevision = kind == "object" ? 0 : 1,
            MoveActionId = kind == "move" ? 0 : _action });
        Assert.False(Assert.Single(_messages.OfType<PickupItemRt>()).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Equal(8, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
    }

    /// <summary>走近期间对象变化、背包变化或新的移动都取消原意图。</summary>
    [Theory]
    [InlineData("remove")]
    [InlineData("revision")]
    [InlineData("bag")]
    [InlineData("move")]
    [InlineData("session")]
    public void PendingPickupCannotSurviveChangedContext(string kind)
    {
        var item = Ground(x: 1800);
        Begin(item);
        switch (kind)
        {
            case "remove": _server.RemoveMapObject(1, item.ObjectId); break;
            case "revision": _server.UpdateMapObject(1, item with { Count = 4 }); break;
            case "bag": Send(new Gm { RoleId = _id, Command = "item 2000 1" }); break;
            case "move": Send(new Move { RoleId = _id, ActionId = ++_action, X = 1500, Y = 1500 }); break;
            case "session": _server.ResetSession(2); break;
        }
        _server.Tick(10);
        Assert.DoesNotContain(_messages.OfType<PickupItemRt>(), result => result.Success);
        Assert.DoesNotContain(_messages.OfType<BagChangedNotice>(), notice => notice.Bag.Items.Sum(item => item.Count) > 9);
    }

    /// <summary>反向转移最终提交失败恢复旧地面与旧背包，最新内存可重试，重启仅恢复一次。</summary>
    [Fact]
    public void PickupRollbackRetryAndRestartPreserveBothSides()
    {
        var item = Ground(); _server.Flush();
        Begin(item);
        _server.Transfers.BeforeComplete = () => throw new IOException("模拟提交点失败");
        Assert.Throws<AggregateException>(_server.Flush);
        Assert.Equal(8, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        Assert.Single(new RepoServerMap(_root).Load(_server.GetMap(1).Rules));
        Assert.Empty(_server.GetMap(1).Objects);
        _server.Transfers.BeforeComplete = null;
        _server.Flush();
        _server = Start(); Enter(_id);
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.Equal(13, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
    }

    /// <summary>没有落地的拾取异常退出时，恢复旧地面物品及旧背包。</summary>
    [Fact]
    public void UnflushedPickupRestoresOldState()
    {
        var item = Ground(); _server.Flush();
        Begin(item);
        _server = Start(); Enter(_id);
        Assert.Single(_server.GetMap(1).Objects);
        Assert.Equal(8, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
    }

    /// <summary>同周期丢弃再拾取仍保持守恒，不留下孤立地图状态。</summary>
    [Fact]
    public void DropAndPickupInSameCyclePreserveTotal()
    {
        Send(new DropItem { RoleId = _id, InstanceId = 1, Count = 2, ExpectedRevision = 1 });
        var item = Assert.Single(_server.GetMap(1).Objects).Value;
        Begin(item, 2);
        Assert.True(Assert.Single(_messages.OfType<PickupItemRt>()).Success);
        new MockTransport(_server).Dispose();
        Assert.Equal(8, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        _server = Start(); Enter(_id);
        Assert.Empty(_server.GetMap(1).Objects);
    }

    /// <summary>不同角色先丢后捡共享同一组，失败不能只保存一人的转移结果。</summary>
    [Fact]
    public void TransferBetweenRolesRemainsOneCommitGroup()
    {
        var second = _repo.Create("接收角色", ServerRoleGender.Female, 1, spawnX: 1500, spawnY: 1500);
        _server = Start(); Enter(_id);
        Send(new DropItem { RoleId = _id, InstanceId = 1, Count = 3, ExpectedRevision = 1 });
        var item = Assert.Single(_server.GetMap(1).Objects).Value;
        Enter(second.RoleId); Begin(item, 0, second.RoleId);
        _server.Transfers.BeforeComplete = () => throw new IOException("模拟跨角色失败");
        Assert.Throws<AggregateException>(_server.Flush);
        Assert.Equal(8, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        Assert.Empty(_repo.Load(second.RoleId).Bag.Items);
        _server.Transfers.BeforeComplete = null; _server.Flush();
        Assert.Equal(5, _repo.Load(_id).Bag.Items.Sum(item => item.Count));
        Assert.Equal(3, _repo.Load(second.RoleId).Bag.Items.Sum(item => item.Count));
    }

    /// <summary>只清理独占验收目录。</summary>
    public void Dispose() => Directory.Delete(_root, true);
}
