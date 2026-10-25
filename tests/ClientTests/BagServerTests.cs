using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/gm-item 与 role-bag 的字节链路、原子发放、兼容与重启持久化。</summary>
public sealed class BagServerTests : IDisposable
{
    /// <summary>本次测试独占的临时存档。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-bag-server-" + Guid.NewGuid());
    /// <summary>实际服务端发出的完整解码消息。</summary>
    private readonly List<IMessage> _messages = [];
    /// <summary>成功提交的目标文件名，用于领域写入隔离验收。</summary>
    private readonly List<string> _writes = [];
    /// <summary>测试使用的独立规则，普通道具不会写入正式配置。</summary>
    private readonly DataServerBagRules _rules = new(2, 2,
        [new(1012, "测试剑", 1), new(2000, "临时堆叠道具", 10)]);
    /// <summary>通过真实 protobuf 信封处理请求的模拟服务端。</summary>
    private MockServer _server;
    /// <summary>便于独立核对磁盘状态的角色仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>本次创建的测试角色。</summary>
    private readonly long _roleId;

    /// <summary>建立空角色、独立配置和字节接收观察，不依赖客户端或 Godot。</summary>
    public BagServerTests()
    {
        _repo = new RepoServerRole(_directory);
        _roleId = _repo.Create("背包测试", ServerRoleGender.Male, 1).RoleId;
        _server = CreateServer();
    }

    /// <summary>独立重建服务端并绑定真正的输出字节边界。</summary>
    private MockServer CreateServer()
    {
        var server = new MockServer(_directory, _rules, (path, _) => _writes.Add(Path.GetFileName(path)));
        server.ResetSession(1);
        server.Outgoing += bytes => _messages.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false));
        return server;
    }

    /// <summary>通过真实请求登录、选角，选角只加载已有存档。</summary>
    private void Enter()
    {
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = _roleId });
        Assert.True(_messages.OfType<SelectRoleRt>().Last().Success);
        _messages.Clear();
    }

    /// <summary>发送独立字节包，不能直接共享业务数据对象。</summary>
    private void Send(IMessage message) => _server.Receive(MessageCodec.Encode(message, 1, true));

    /// <summary>发放命令并取得本次响应。</summary>
    private GmRt Grant(string command, long? roleId = null)
    {
        _messages.Clear();
        Send(new Gm { RoleId = roleId ?? _roleId, Command = command });
        _server.Flush(); // 显式落地节点后核对业务结果与文件。
        return Assert.Single(_messages.OfType<GmRt>());
    }

    /// <summary>通过真实请求移动指定实例，允许构造旧版本或非法角色回归。</summary>
    private MoveItemRt MoveInstance(long instanceId, int targetSlot, long? revision = null, long? roleId = null)
    {
        _messages.Clear();
        Send(new MoveItem { RoleId = roleId ?? _roleId, InstanceId = instanceId,
            TargetSlotIndex = targetSlot, ExpectedRevision = revision ?? _repo.Load(_roleId).Bag.Revision });
        _server.Flush(); // 在明确的验收节点核对磁盘。
        return Assert.Single(_messages.OfType<MoveItemRt>());
    }

    /// <summary>mock-server：真实 GM、移动与物品移动只提交各自领域，失败和同格不记录 IO。</summary>
    [Fact]
    public void BusinessOperationsWriteOnlyTheirDomain()
    {
        Enter();
        var roleBytes = File.ReadAllBytes(_repo.GetRolePath(_roleId));
        Assert.True(Grant("item 1012").Success);
        Assert.Equal(new[] { $"{_roleId}.bag.json" }, _writes);
        Assert.Equal(roleBytes, File.ReadAllBytes(_repo.GetRolePath(_roleId)));
        var bagBytes = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        _writes.Clear();
        Send(new Move { RoleId = _roleId, ActionId = 1, X = 100, Y = 120 });
        _server.Tick(0.5);
        _server.Flush();
        Assert.True(Assert.Single(_messages.OfType<MoveRt>()).Success);
        Assert.Equal(new[] { $"{_roleId}.role.json" }, _writes);
        Assert.Equal(bagBytes, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
        _writes.Clear();
        Assert.True(MoveInstance(1, 3).Success);
        Assert.Equal(new[] { $"{_roleId}.bag.json" }, _writes);
        _writes.Clear();
        Assert.True(MoveInstance(1, 3).Success);
        Assert.False(Grant("item 9999").Success);
        Assert.Empty(_writes);
    }

    /// <summary>role-bag：空格移动和占用交换保留实例身份、数量与编号，角色移动与重启不覆盖背包。</summary>
    [Fact]
    public void MovesAndSwapsPersistWithoutChangingIdentityOrQuantity()
    {
        Enter();
        Assert.True(Grant("item 1012 2").Success);
        Assert.True(Grant("item 2000 3").Success);
        var original = _repo.Load(_roleId).Bag;
        var sword = original.Items[0];
        Assert.True(MoveInstance(sword.InstanceId, 3).Success);
        var notice = Assert.Single(_messages.OfType<BagChangedNotice>());
        Assert.Equal(3, notice.Bag.Items.Single(item => item.InstanceId == sword.InstanceId).SlotIndex);
        Assert.True(MoveInstance(sword.InstanceId, 2).Success);
        var saved = _repo.Load(_roleId).Bag;
        Assert.Equal(2, saved.Items.Single(item => item.InstanceId == sword.InstanceId).SlotIndex);
        Assert.Equal(3, saved.Items.Single(item => item.ItemId == 2000).SlotIndex);
        Assert.Equal(original.NextInstanceId, saved.NextInstanceId);
        Assert.Equal(original.Revision + 2, saved.Revision);
        Assert.Equal(original.Items.OrderBy(item => item.InstanceId).Select(item => (item.InstanceId, item.ItemId, item.Count)),
            saved.Items.OrderBy(item => item.InstanceId).Select(item => (item.InstanceId, item.ItemId, item.Count)));
        Send(new Move { RoleId = _roleId, ActionId = 1, X = 200, Y = 100 });
        _server.Tick(0.5);
        _server.Flush();
        Assert.Equal(saved, _repo.Load(_roleId).Bag);
        _server = CreateServer();
        Enter();
        Send(new SelectRole { RoleId = _roleId });
        Assert.Equal(saved.Items.Select(item => item.SlotIndex), _messages.OfType<SelectRoleRt>().Last().Bag.Items.Select(item => item.SlotIndex));
    }

    /// <summary>role-bag：部分合并剩余留原格，完全合并只删除源实例，满堆叠执行交换。</summary>
    [Theory]
    [InlineData(3, 8, 1, 10, 2)]
    [InlineData(2, 8, 0, 10, 1)]
    [InlineData(3, 10, 10, 3, 2)]
    public void MergesStacksOrSwapsFullTarget(int sourceCount, int targetCount, int expectedSourceCount, int expectedTargetCount, int itemCount)
    {
        _repo.Save(_repo.Load(_roleId) with { Bag = new DataServerBag { NextInstanceId = 3,
            Items = [new(1, 2000, 0, sourceCount), new(2, 2000, 1, targetCount)] } });
        Enter();
        Assert.True(MoveInstance(1, 1).Success);
        var bag = _repo.Load(_roleId).Bag;
        Assert.Equal(itemCount, bag.Items.Count);
        Assert.Equal(expectedSourceCount, bag.Items.FirstOrDefault(item => item.SlotIndex == 0)?.Count ?? 0);
        Assert.Equal(expectedTargetCount, bag.Items.Single(item => item.SlotIndex == 1).Count);
        Assert.Equal(sourceCount + targetCount, bag.Items.Sum(item => item.Count));
        Assert.Equal(3, bag.NextInstanceId);
        Assert.Equal(1, bag.Revision);
        if (sourceCount <= 10 - targetCount)
        {
            Assert.Equal(2, Assert.Single(bag.Items).InstanceId);
            Assert.True(Grant("item 1012").Success);
            Assert.Equal(3, _repo.Load(_roleId).Bag.Items.Single(item => item.ItemId == 1012).InstanceId);
        }
        _server = CreateServer();
        Enter();
        Assert.Equal(sourceCount + targetCount, _repo.Load(_roleId).Bag.Items.Where(item => item.ItemId == 2000).Sum(item => item.Count));
    }

    /// <summary>role-bag：满容量仍可移动或交换，同格不写盘、不推送、不递增版本。</summary>
    [Fact]
    public void FullBagCanSwapAndSameSlotDoesNothing()
    {
        Enter();
        Assert.True(Grant("item 1012 4").Success);
        var bytes = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        Assert.True(MoveInstance(1, 0).Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Equal(bytes, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
        Assert.True(MoveInstance(1, 3).Success);
        Assert.Equal(3, _repo.Load(_roleId).Bag.Items.Single(item => item.InstanceId == 1).SlotIndex);
        Assert.Equal(0, _repo.Load(_roleId).Bag.Items.Single(item => item.InstanceId == 4).SlotIndex);
    }

    /// <summary>role-bag：身份、实例、范围、旧版本与 GM 更新后的旧拖放均被服务端拒绝。</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("outside")]
    [InlineData("revision")]
    public void InvalidMovementDoesNotChangeState(string kind)
    {
        Assert.False(MoveInstance(1, 0, 0).Success);
        Enter();
        Grant("item 1012");
        var before = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        var result = MoveInstance(kind == "missing" ? 999 : 1,
            kind == "negative" ? -1 : kind == "outside" ? 4 : 2,
            kind == "revision" ? 0 : 1, kind == "identity" ? _roleId + 1 : _roleId);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Error);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Equal(before, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
        Assert.True(MoveInstance(1, 2).Success);
        var moved = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        Assert.False(MoveInstance(1, 3, 1).Success);
        Assert.Equal(moved, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
    }

    /// <summary>server-write-back：移动先提交内存，落地失败不回退版本，恢复后补写。</summary>
    [Fact]
    public void FailedMoveFlushRetainsLatestSlotAndVersion()
    {
        Enter();
        Grant("item 1012");
        Send(new MoveItem { RoleId = _roleId, InstanceId = 1, TargetSlotIndex = 3, ExpectedRevision = 1 });
        Assert.True(Assert.Single(_messages.OfType<MoveItemRt>()).Success);
        using (var locked = new FileStream(_repo.GetBagPath(_roleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => _server.Flush());
        Assert.Equal(0, Assert.Single(_repo.Load(_roleId).Bag.Items).SlotIndex);
        _server.Flush();
        Assert.Equal(3, Assert.Single(_repo.Load(_roleId).Bag.Items).SlotIndex);
        Assert.Equal(2, _repo.Load(_roleId).Bag.Revision);
    }

    /// <summary>装备逐件分配、默认数量与跨格堆叠优先补满，并经重启保持实例。</summary>
    [Fact]
    public void EquipmentAndStacksPersistAcrossRestartAndMovement()
    {
        Enter();
        Assert.True(Grant("item 1012").Success);
        Assert.Equal(1, Assert.Single(_messages.OfType<BagChangedNotice>()).Bag.Items[0].Count);
        Assert.True(Grant("item 2000 13").Success);
        var previous = _repo.Load(_roleId).Bag;
        Assert.Equal(new[] { 1, 10, 3 }, previous.Items.Select(item => item.Count));
        Assert.True(Grant("item 2000 8").Success);
        var saved = _repo.Load(_roleId).Bag;
        Assert.Equal(new[] { 1, 10, 10, 1 }, saved.Items.Select(item => item.Count));
        Assert.Equal(previous.Items[2].InstanceId, saved.Items[2].InstanceId);
        Send(new Move { RoleId = _roleId, ActionId = 1, X = 100, Y = 120 });
        _server.Tick(0.5);
        _server.Flush();
        Assert.Equal(saved.Items, _repo.Load(_roleId).Bag.Items);
        Assert.Null(MoveRt.Descriptor.FindFieldByName("bag"));
        _server = CreateServer();
        Enter();
        Assert.Equal(saved.Items, _repo.Load(_roleId).Bag.Items);
        Send(new SelectRole { RoleId = _roleId });
        Assert.Equal(saved.Revision, _messages.OfType<SelectRoleRt>().Last().Bag.Revision);
    }

    /// <summary>多件装备各占一格，容量不足整单失败，不消耗编号或版本。</summary>
    [Fact]
    public void CapacityFailureLeavesFileAndIdentifiersUnchanged()
    {
        Enter();
        Assert.True(Grant("item 1012 2").Success);
        var before = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        Assert.False(Grant("item 1012 3").Success);
        Assert.Empty(_messages.OfType<BagChangedNotice>());
        Assert.Equal(before, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
        Assert.True(Grant("item 1012 2").Success);
        var bag = _repo.Load(_roleId).Bag;
        Assert.Equal(new long[] { 1, 2, 3, 4 }, bag.Items.Select(item => item.InstanceId));
        Assert.Equal(2, bag.Revision);
    }

    /// <summary>server-write-back：发放成功先推送，失败落地重试不重复发放或消耗编号。</summary>
    [Fact]
    public void FlushFailureKeepsGrantedMemoryAndRetries()
    {
        Enter();
        Send(new Gm { RoleId = _roleId, Command = "item 1012" });
        Assert.True(Assert.Single(_messages.OfType<GmRt>()).Success);
        Assert.Single(_messages.OfType<BagChangedNotice>());
        using (var locked = new FileStream(_repo.GetBagPath(_roleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => _server.Flush());
        Assert.Empty(_repo.Load(_roleId).Bag.Items);
        _server.Flush();
        var bag = _repo.Load(_roleId).Bag;
        Assert.Equal(1, bag.Revision);
        Assert.Equal(1, Assert.Single(bag.Items).InstanceId);
        Assert.Equal(2, bag.NextInstanceId);
    }

    /// <summary>身份与错误参数均在服务端拒绝，即使绕过客户端输入。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("unknown 1012")]
    [InlineData("item")]
    [InlineData("item 1012 1 extra")]
    [InlineData("item 9999")]
    [InlineData("item -1")]
    [InlineData("item 1012 0")]
    [InlineData("item 1012 -1")]
    [InlineData("item 1012 2147483648")]
    [InlineData("item 1012 1.5")]
    public void RejectsInvalidCommandsAndIdentity(string command)
    {
        Assert.False(Grant("item 1012").Success);
        Enter();
        var before = File.ReadAllBytes(_repo.GetBagPath(_roleId));
        Assert.False(Grant(command).Success);
        Assert.False(Grant("item 1012", _roleId + 1).Success);
        Assert.Equal(before, File.ReadAllBytes(_repo.GetBagPath(_roleId)));
    }

    /// <summary>旧存档缺失背包字段时为空，额外行数影响真实可发放容量。</summary>
    [Fact]
    public void LegacySaveDefaultsToEmptyAndExtraRowsExpandCapacity()
    {
        var path = _repo.GetLegacyPath(_roleId);
        var json = System.Text.Json.JsonSerializer.SerializeToNode(_repo.Load(_roleId))!.AsObject();
        File.Delete(_repo.GetRolePath(_roleId));
        File.Delete(_repo.GetBagPath(_roleId));
        json.Remove("Bag");
        json.Remove("BagExtraRows");
        File.WriteAllText(path, json.ToJsonString());
        var old = _repo.Load(_roleId);
        Assert.Empty(old.Bag.Items);
        Assert.Equal(0, old.BagExtraRows);
        _repo.Save(old with { BagExtraRows = 1 });
        Enter();
        Assert.True(Grant("item 1012 6").Success);
        Assert.False(Grant("item 1012").Success);
    }

    /// <summary>新增请求、响应和推送方向都显式注册，往返不共享对象。</summary>
    [Fact]
    public void GmAndBagProtocolsCheckDirectionAndCopyPayload()
    {
        IMessage[] messages = [new MoveItem { RoleId = 1, InstanceId = 2, TargetSlotIndex = 3, ExpectedRevision = 4 },
            new MoveItemRt { RoleId = 1, InstanceId = 2, TargetSlotIndex = 3, Success = true },
            new Gm { RoleId = 1, Command = "item 1012" },
            new GmRt { RoleId = 1, Success = true, Message = "成功" },
            new BagChangedNotice { RoleId = 1, Bag = new BagPb { Revision = 1,
                Items = { new ItemPb { InstanceId = 1, ItemId = 1012, Count = 1, SlotIndex = 0 } } } }];
        foreach (var message in messages)
        {
            var request = message is Gm or MoveItem;
            var decoded = MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(MessageCodec.Encode(message, 1, request)), request);
            Assert.Equal(message, decoded);
            Assert.NotSame(message, decoded);
            Assert.Throws<InvalidDataException>(() => MessageCodec.Encode(message, 1, !request));
        }
    }

    /// <summary>损坏存档在选角时整体拒绝，不静默删掉超量或未知道具。</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("stack")]
    [InlineData("slot")]
    [InlineData("duplicate")]
    public void CorruptBagIsRejectedAtSelection(string kind)
    {
        var item = new DataServerItem(1, kind == "unknown" ? 9999 : 1012,
            kind == "slot" ? 4 : 0, kind == "stack" ? 2 : 1);
        var items = kind == "duplicate" ? new[] { item, item with { InstanceId = 2 } } : new[] { item };
        var path = _repo.GetBagPath(_roleId);
        var role = _repo.Load(_roleId) with { Bag = new DataServerBag { NextInstanceId = 3, Items = items } };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(DataServerBagFile.FromRole(role)));
        var before = File.ReadAllBytes(path);
        Send(new Login { Username = "demo", Password = "demo" });
        Send(new SelectRole { RoleId = _roleId });
        Assert.False(_messages.OfType<SelectRoleRt>().Last().Success);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>释放仅属于本次测试的临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
