using Google.Protobuf;
using System.Numerics;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/ground-item-drop 的物品守恒、权威请求及共同落地恢复。</summary>
public sealed class GroundItemDropTests : IDisposable
{
    /// <summary>验收独占的服务端目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-drop-" + Guid.NewGuid());
    /// <summary>真实 protobuf 输出。</summary>
    private readonly List<IMessage> _messages = [];
    /// <summary>完成提交的业务日志。</summary>
    private readonly List<string> _logs = [];
    /// <summary>两张地图使用相同尺寸和相互独立的状态。</summary>
    private readonly DataServerMapRules[] _maps = [new(1, 100, 100, 32, 32), new(2, 100, 100, 32, 32)];
    /// <summary>包含可部分丢弃的测试堆叠。</summary>
    private readonly DataServerBagRules _rules = new(8, 5, [new(2000, "验收道具", 10)]);
    /// <summary>便于从文件核对两侧状态的磁盘仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>当前测试角色。</summary>
    private readonly long _id;
    /// <summary>被测单进程服务端。</summary>
    private MockServer _server;

    /// <summary>创建已落地的五件堆叠及真实网络会话。</summary>
    public GroundItemDropTests()
    {
        _repo = new RepoServerRole(_root);
        var role = _repo.Create("丢弃测试", ServerRoleGender.Male, 1, spawnX: 1500, spawnY: 1500);
        _id = role.RoleId;
        _repo.SaveBag(role with { Bag = new() { Revision = 1, NextInstanceId = 2, Items = [new(1, 2000, 0, 5)] } });
        _server = Start();
        Enter(_id);
        _messages.Clear();
    }

    /// <summary>重建不隐式关闭旧服务端，以模拟异常退出。</summary>
    private MockServer Start()
    {
        var server = new MockServer(_root, _rules, (path, _) => _logs.Add(path), () => 0, _maps);
        server.ResetSession(1);
        server.Outgoing += bytes => _messages.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false));
        return server;
    }

    /// <summary>请求始终经过显式注册的字节边界。</summary>
    private void Send(IMessage message) => _server.Receive(MessageCodec.Encode(message, 1, true));

    /// <summary>登录并选中指定角色，验证恢复流程可正常读取。</summary>
    private void Enter(long id)
    {
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = id });
        Assert.True(_messages.OfType<SelectRoleRt>().Last().Success);
    }

    /// <summary>构造可指定旧版本、非法数量和身份的请求。</summary>
    private DropItemRt Drop(int count, long revision = 1, long? role = null, long instance = 1, bool destroy = false, Vector2? position = null)
    {
        _messages.Clear();
        var request = new DropItem { RoleId = role ?? _id, InstanceId = instance, Count = count, ExpectedRevision = revision, Destroy = destroy };
        if (position is { } point) { request.DropX = point.X; request.DropY = point.Y; }
        Send(request);
        return Assert.Single(_messages.OfType<DropItemRt>());
    }

    /// <summary>部分丢弃保留身份，全部丢弃移除源，地面数量与原背包守恒。</summary>
    [Fact]
    public void PartialThenFullDropPreservesTotalAndIdentity()
    {
        Assert.True(Drop(2, position: new Vector2(1550, 1450)).Success);
        var bag = Assert.Single(_messages.OfType<BagChangedNotice>()).Bag;
        Assert.Equal(3, Assert.Single(bag.Items).Count);
        Assert.Equal(1, bag.Items[0].InstanceId);
        Assert.Single(_messages.OfType<MapAoiNotice>());
        var ground = Assert.Single(_server.GetMap(1).Objects).Value;
        AssertDropClearance(ground, 1500, 1500);
        Assert.Equal(2, ground.Count);
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        Assert.True(Drop(3, 2, position: new Vector2(1450, 1550)).Success);
        Assert.Empty(Assert.Single(_messages.OfType<BagChangedNotice>()).Bag.Items);
        var dropped = _server.GetMap(1).Objects.Values.ToArray();
        Assert.Equal(5, dropped.Sum(item => item.Count));
        Assert.Contains(dropped, item => item.X == 1550 && item.Y == 1450);
        Assert.Contains(dropped, item => item.X == 1450 && item.Y == 1550);
        // ground-item-drop：两次丢弃的真实坐标投影后须让 32 像素图标分开。
        var dx = dropped[0].X - dropped[1].X;
        var dy = dropped[0].Y - dropped[1].Y;
        var screenDistance = new Vector2((dx - dy) * 0.5f, (dx + dy) * 0.25f).Length();
        Assert.True(screenDistance >= 40f, $"连续丢弃间距过小：{screenDistance}");
        _server.Flush();
        Assert.Empty(_repo.Load(_id).Bag.Items);
        Assert.Equal(2, _repo.Load(_id).Bag.NextInstanceId);
        _server = Start(); Enter(_id);
        Assert.Equal(5, _server.GetMap(1).Objects.Values.Sum(item => item.Count));
        Assert.Empty(_server.GetMap(2).Objects);
    }

    /// <summary>对应 ground-item-drop：客户端提议越界或偏离权威角色时不扣背包。</summary>
    [Theory]
    [InlineData(1500, 1500)]
    [InlineData(1650, 1500)]
    [InlineData(1400, 1400)]
    [InlineData(-1, 1500)]
    public void InvalidClientDropPositionIsRejected(float x, float y)
    {
        Assert.False(Drop(1, position: new Vector2(x, y)).Success);
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
    }

    /// <summary>对应 ground-item-drop：不完整坐标不得绕过显式落点校验。</summary>
    [Fact]
    public void PartialClientDropPositionIsRejected()
    {
        _messages.Clear();
        Send(new DropItem { RoleId = _id, InstanceId = 1, Count = 1, ExpectedRevision = 1, DropX = 1550 });
        Assert.False(Assert.Single(_messages.OfType<DropItemRt>()).Success);
        Assert.Empty(_server.GetMap(1).Objects);
    }

    /// <summary>对应周围避让与边缘场景：四边和四角仍保留完整距离并位于地图内。</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3199, 0)]
    [InlineData(0, 3199)]
    [InlineData(3199, 3199)]
    [InlineData(0, 1500)]
    [InlineData(3199, 1500)]
    [InlineData(1500, 0)]
    [InlineData(1500, 3199)]
    public void DropAroundMapEdgesAvoidsBody(float x, float y)
    {
        _server.PublishRoleState(_repo.Load(_id) with { X = x, Y = y });
        Assert.True(Drop(1).Success);
        var item = Assert.Single(_server.GetMap(1).Objects).Value;
        AssertDropClearance(item, x, y);
        Assert.True(_maps[0].Contains(item.X, item.Y));
    }

    /// <summary>验证画面距离和身体避让区域，不依赖随机选择的具体方向。</summary>
    private static void AssertDropClearance(DataServerMapObject item, float x, float y)
    {
        var dx = item.X - x;
        var dy = item.Y - y;
        var screenX = (dx - dy) * 0.5f;
        var screenY = (dx + dy) * 0.25f;
        Assert.InRange(MathF.Sqrt(screenX * screenX + screenY * screenY), 47.99f, 56.01f);
        Assert.True(MathF.Abs(screenX) >= 39.99f || screenY >= 23.99f);
    }

    /// <summary>非法和重复请求不递增版本，不生成对象或成功通知。</summary>
    [Theory]
    [InlineData(0, 1, false, 1)]
    [InlineData(-1, 1, false, 1)]
    [InlineData(6, 1, false, 1)]
    [InlineData(1, 0, false, 1)]
    [InlineData(1, 1, true, 1)]
    [InlineData(1, 1, false, 99)]
    public void InvalidRequestDoesNotMutate(int count, long version, bool wrongRole, long instance)
    {
        Assert.False(Drop(count, version, wrongRole ? _id + 1 : _id, instance).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.True(Drop(1).Success);
        Assert.False(Drop(1).Success);
        Assert.Single(_server.GetMap(1).Objects);
    }

    /// <summary>对应 Ctrl 销毁的部分/全部场景：不产生 AOI 或对象，拒绝重放，关闭落地后不复活。</summary>
    [Fact]
    public void DestroyPartialAndFullPersistsWithoutGroundObjects()
    {
        var response = Drop(2, destroy: true);
        Assert.True(response.Success);
        Assert.Empty(response.ObjectId);
        var bag = Assert.Single(_messages.OfType<BagChangedNotice>()).Bag;
        Assert.Equal((1L, 3), (Assert.Single(bag.Items).InstanceId, bag.Items[0].Count));
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.False(Drop(2, destroy: true).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.True(Drop(3, 2, destroy: true).Success);
        Assert.Empty(Assert.Single(_messages.OfType<BagChangedNotice>()).Bag.Items);
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        _server.Flush();
        _server = Start(); Enter(_id);
        Assert.Empty(_messages.OfType<SelectRoleRt>().Last().Bag.Items);
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.Equal(2, _repo.Load(_id).Bag.NextInstanceId);
    }

    /// <summary>对应非法销毁场景：数量、身份、实例及版本均必须校验。</summary>
    [Theory]
    [InlineData(0, 1, false, 1)]
    [InlineData(-1, 1, false, 1)]
    [InlineData(6, 1, false, 1)]
    [InlineData(1, 0, false, 1)]
    [InlineData(1, 1, true, 1)]
    [InlineData(1, 1, false, 99)]
    public void InvalidDestroyDoesNotMutate(int count, long revision, bool wrongRole, long instance)
    {
        Assert.False(Drop(count, revision, wrongRole ? _id + 1 : _id, instance, true).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.True(Drop(1, destroy: true).Success);
        Assert.Equal(4, Assert.Single(_messages.OfType<BagChangedNotice>()).Bag.Items[0].Count);
    }

    /// <summary>对应同周期转移与销毁场景：关闭后共同恢复实际地面数量和销毁后的背包。</summary>
    [Fact]
    public void DropThenDestroyUsesExistingTransferGroup()
    {
        Assert.True(Drop(2).Success);
        Assert.True(Drop(3, 2, destroy: true).Success);
        Assert.Empty(_messages.OfType<MapAoiNotice>());
        _server.Flush();
        _server = Start(); Enter(_id);
        Assert.Empty(_messages.OfType<SelectRoleRt>().Last().Bag.Items);
        Assert.Equal(2, Assert.Single(_server.GetMap(1).Objects).Value.Count);
    }

    /// <summary>周期禁用且未关闭就重启时，两侧都回到上次完整状态。</summary>
    [Fact]
    public void UnflushedDropRestoresBothOldSides()
    {
        Assert.True(Drop(2).Success);
        _server.Tick(60);
        _server = Start(); Enter(_id);
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.Equal(5, Assert.Single(_messages.OfType<SelectRoleRt>().Last().Bag.Items).Count);
    }

    /// <summary>写完背包但地图替换失败会回滚，其他独立地图仍能落地，修复后可重试。</summary>
    [Fact]
    public void MapFailureRollsBackBagButRetainsMemoryForRetry()
    {
        Assert.True(Drop(2).Success);
        var independent = _server.CreateMapObject(2, new() { X = 1, Y = 1, MonsterTypeId = "test" });
        var path = new RepoServerMap(_root).ManifestPath(1);
        Directory.CreateDirectory(path); // 用目录阻止清单替换，但回滚原先无清单无需删除目录。
        Assert.Throws<AggregateException>(_server.Flush);
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        Assert.Equal(2, Assert.Single(_server.GetMap(1).Objects).Value.Count);
        Assert.True(File.Exists(new RepoServerMap(_root).ManifestPath(2)));
        Assert.DoesNotContain(_logs, log => log == _repo.GetBagPath(_id));
        Directory.Delete(path);
        _server.Flush();
        Assert.Equal(3, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        _server = Start(); Enter(_id);
        Assert.Equal(2, Assert.Single(_server.GetMap(1).Objects).Value.Count);
        Assert.Equal(independent.ObjectId, Assert.Single(_server.GetMap(2).Objects).Key);
    }

    /// <summary>清单已替换但最终提交点失败，回滚后旧引用块仍存在且重试成功。</summary>
    [Fact]
    public void FinalCommitFailureKeepsOldChunksAndLatestDirtyMemory()
    {
        var before = _server.CreateMapObject(1, new() { X = 1500, Y = 1500, ItemId = 2000, Count = 1 });
        _server.Flush();
        Assert.True(Drop(2).Success);
        _server.UpdateMapObject(1, before with { X = 1 });
        _server.Transfers.BeforeComplete = () => throw new IOException("模拟最后提交点失败");
        Assert.Throws<AggregateException>(_server.Flush);
        var saved = Assert.Single(new RepoServerMap(_root).Load(_maps[0]));
        Assert.Equal(1500, saved.X);
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        _server.Transfers.BeforeComplete = null;
        _server.Flush();
        Assert.Equal(2, new RepoServerMap(_root).Load(_maps[0]).Count);
        Assert.Equal(3, Assert.Single(_repo.Load(_id).Bag.Items).Count);
    }

    /// <summary>模拟进程在写完两侧但尚有回滚记录时退出，启动先恢复旧组再读取。</summary>
    [Fact]
    public void StartupRecoversInterruptedGroupBeforeLoading()
    {
        var transfer = new RepoServerItemTransfer(_root);
        transfer.Begin(1, [_id]);
        var original = _repo.Load(_id);
        _repo.SaveBag(original with { Bag = original.Bag with { Items = [], Revision = 2 } });
        var repoMap = new RepoServerMap(_root);
        repoMap.Commit(_maps[0], new Dictionary<(int, int), IReadOnlyList<DataServerMapObject>>
        { [(1, 1)] = [new() { X = 1500, Y = 1500, ItemId = 2000, Count = 5 }] }, cleanup: false);
        _server = Start(); Enter(_id);
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        Assert.Empty(_server.GetMap(1).Objects);
        Assert.False(File.Exists(transfer.PendingPath));
    }

    /// <summary>新角色尚未落地也可以丢弃，关闭节点共同创建完整角色和地图存档。</summary>
    [Fact]
    public void NewRoleAndMultipleMutationsFlushTogetherAfterSessionReset()
    {
        _server.ResetSession(1);
        Send(new CreateRole { Name = "新角色", Gender = RoleGenderPb.Female, HeadId = 1 });
        var id = _messages.OfType<CreateRoleRt>().Last().Roles.Single(role => role.Name == "新角色").RoleId;
        Send(new SelectRole { RoleId = id });
        Send(new Gm { RoleId = id, Command = "item 2000 5" });
        Assert.True(Drop(2, 1, id).Success);
        Send(new Gm { RoleId = id, Command = "item 2000 1" });
        _server.ResetSession(2);
        new MockTransport(_server).Dispose(); // 正常关闭补写，即使检测周期为零。
        Assert.Equal(4, Assert.Single(_repo.Load(id).Bag.Items).Count);
        Assert.Equal(2, Assert.Single(new RepoServerMap(_root).Load(_maps[0])).Count);
    }

    /// <summary>落点使用服务端移动中间位置，不采用旧角色确认坐标。</summary>
    [Fact]
    public void DropUsesActualPositionDuringMovement()
    {
        Send(new Move { RoleId = _id, ActionId = 1, X = 2500, Y = 1500 });
        _server.Tick(0.2);
        Assert.True(Drop(2).Success);
        var ground = Assert.Single(_server.GetMap(1).Objects).Value;
        AssertDropClearance(ground, 1536, 1500);
    }

    /// <summary>旧聚合存档迁移失败恢复旧格式，重试成功后两侧一起迁移。</summary>
    [Fact]
    public void LegacyMigrationParticipatesInSameRollbackBoundary()
    {
        File.WriteAllText(_repo.GetLegacyPath(_id), System.Text.Json.JsonSerializer.Serialize(_repo.Load(_id)));
        File.Delete(_repo.GetRolePath(_id));
        File.Delete(_repo.GetBagPath(_id));
        _server = Start(); Enter(_id);
        Assert.True(Drop(2).Success);
        _server.Transfers.BeforeComplete = () => throw new IOException("模拟迁移提交失败");
        Assert.Throws<AggregateException>(_server.Flush);
        Assert.False(File.Exists(_repo.GetRolePath(_id)));
        Assert.False(File.Exists(_repo.GetBagPath(_id)));
        Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        _server.Transfers.BeforeComplete = null;
        _server.Flush();
        Assert.True(File.Exists(_repo.GetLegacyPath(_id)));
        Assert.Equal(3, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        _server = Start(); Enter(_id);
        Assert.Equal(2, Assert.Single(_server.GetMap(1).Objects).Value.Count);
    }

    /// <summary>同地图多角色关联为一组，第二个背包锁住时连同第一个回滚，解锁后共同重试。</summary>
    [Fact]
    public void MultipleRolesOnSameMapCannotPartiallyFlush()
    {
        var second = _repo.Create("另一角色", ServerRoleGender.Female, 1, spawnX: 1500, spawnY: 1500);
        _repo.SaveBag(second with { Bag = new() { Revision = 1, NextInstanceId = 2, Items = [new(1, 2000, 0, 5)] } });
        _server = Start(); Enter(_id);
        Assert.True(Drop(2).Success);
        Enter(second.RoleId);
        Assert.True(Drop(3, role: second.RoleId).Success);
        using (var locked = new FileStream(_repo.GetBagPath(second.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<AggregateException>(_server.Flush);
            Assert.Equal(5, Assert.Single(_repo.Load(_id).Bag.Items).Count);
            Assert.Equal(5, Assert.Single(_repo.Load(second.RoleId).Bag.Items).Count);
            Assert.Empty(new RepoServerMap(_root).Load(_maps[0]));
        }
        _server.Flush();
        Assert.Equal(3, Assert.Single(_repo.Load(_id).Bag.Items).Count);
        Assert.Equal(2, Assert.Single(_repo.Load(second.RoleId).Bag.Items).Count);
        Assert.Equal(5, _server.GetMap(1).Objects.Values.Sum(item => item.Count));
    }

    /// <summary>外层恢复优先于角色迁移恢复，未提交新角色的内部回滚文件不会复活部分数据。</summary>
    [Fact]
    public void InterruptedCreationRemovesNestedPendingBeforeRoleRead()
    {
        var transfer = new RepoServerItemTransfer(_root);
        transfer.Begin(1, [9000]);
        foreach (var domain in new[] { "role", "bag", "pending" })
            File.WriteAllText(Path.Combine(_root, $"9000.{domain}.json"), "未完成文件");
        _server = Start(); Enter(_id);
        Assert.False(File.Exists(Path.Combine(_root, "9000.pending.json")));
        Assert.Single(_repo.List());
    }

    /// <summary>篡改回滚路径必须拒绝，记录保留且不读取部分业务状态。</summary>
    [Fact]
    public void CorruptRecoveryBlocksStartup()
    {
        var transfer = new RepoServerItemTransfer(_root);
        transfer.Begin(1, [_id]);
        File.WriteAllText(transfer.PendingPath, "{\"FormatVersion\":1,\"MapId\":1,\"Roles\":[1],\"Originals\":{\"../escape\":null}}");
        Assert.Throws<IOException>(() => Start());
        Assert.True(File.Exists(transfer.PendingPath));
    }

    /// <summary>清理验收目录，不接触正式用户文件。</summary>
    public void Dispose() => Directory.Delete(_root, true);
}
