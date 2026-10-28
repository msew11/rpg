using Google.Protobuf;
using Rpg.Config.Data;
using Rpg.Const;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Session.Data;

namespace ClientTests;

/// <summary>验证 openspec/specs/role-equipment、client-network 的公开装备契约与独立转换。</summary>
public sealed class PublicEquipmentTests
{
    /// <summary>独立装备转换只需要静态分类，不依赖私有背包或角色数据。</summary>
    private static readonly IReadOnlyDictionary<int, ItemCfg> Definitions = new Dictionary<int, ItemCfg>
    {
        [1012] = new() { Id = 1012, Name = "剑", Category = ItemCategory.Weapon,
            SubCategory = ItemSubCategory.Sword, RelatedId = 1012, MaxStack = 1 },
        [1013] = new() { Id = 1013, RelatedId = 1013, Category = ItemCategory.Weapon, SubCategory = ItemSubCategory.TwoHandedWeapon, MaxStack = 1 },
        [1014] = new() { Id = 1014, RelatedId = 1014, Category = ItemCategory.Armor, SubCategory = ItemSubCategory.Shield, MaxStack = 1 },
        [1015] = new() { Id = 1015, RelatedId = 1015, Category = ItemCategory.Weapon, SubCategory = ItemSubCategory.Staff, MaxStack = 1 },
        [1016] = new() { Id = 1016, RelatedId = 1016, Category = ItemCategory.Weapon, SubCategory = ItemSubCategory.Spear, MaxStack = 1 },
        [1017] = new() { Id = 1017, RelatedId = 1017, Category = ItemCategory.Weapon, SubCategory = ItemSubCategory.Katar, MaxStack = 1 },
        [1018] = new() { Id = 1018, RelatedId = 1018, Category = ItemCategory.Weapon, SubCategory = ItemSubCategory.ThrowingWeapon, MaxStack = 1 }
    };

    /// <summary>公开投影只含配置 ID，信封承担版本；私有实例有穿戴和 oneof 扩展。</summary>
    [Fact]
    public void PublicProjectionAndWireMessagesContainOnlyEquipment()
    {
        var role = new DataServerRole { RoleId = 1001, Bag = new DataServerBag
        {
            NextInstanceId = 3, Items = [new DataServerItem(2, 1012, 17, 1)],
            Equipment = [new DataServerEquipment(1, 1012, EquipmentSlot.MainHand)]
        } };
        var notice = MockServer.CreateEquipmentNotice(role);
        var envelope = MessageCodec.ReadEnvelope(MessageCodec.Encode(notice, 7, false, 23));
        var decoded = Assert.IsType<RoleEquipmentNotice>(MessageCodec.ReadPayload(envelope, false));
        Assert.Equal(notice, decoded);
        Assert.Equal(23, envelope.Revision);
        Assert.Equal(23, decoded.Equipment.GetRevision());
        Assert.Equal(1012, Assert.Single(decoded.Equipment.EquippedItems));
        Assert.Equal(["role_id", "equipped_items"], RoleWorldEquipmentPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        Assert.Equal(["items"], BagPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        Assert.Empty(BagPb.Descriptor.ToProto().ReservedName);
        Assert.Empty(BagPb.Descriptor.ToProto().ReservedRange);
        Assert.Null(RoleWorldProfilePb.Descriptor.FindFieldByName("revision"));
        Assert.Null(RoleWorldPositionPb.Descriptor.FindFieldByName("revision"));
        Assert.Null(RoleWorldStatusPb.Descriptor.FindFieldByName("revision"));
        Assert.Null(RolePb.Descriptor.FindFieldByName("revision"));
        Assert.NotNull(EnvelopePb.Descriptor.FindFieldByName("revision"));
        Assert.Equal(EquipmentPb.Descriptor, ItemPb.Descriptor.FindFieldByName("equipment").MessageType);
        Assert.Empty(EquipmentPb.Descriptor.Fields.InFieldNumberOrder());
        Assert.Single(ItemPb.Descriptor.Oneofs);
        Assert.Null(ItemPb.Descriptor.FindFieldByName("slot"));
        Assert.Equal(BagPb.Descriptor.File, RoleWorldEquipmentPb.Descriptor.File);
        Assert.Equal(BagPb.Descriptor.File, EquipItem.Descriptor.File);
        Assert.Throws<InvalidDataException>(() => MessageCodec.Encode(notice, 7, true));
        var item = new ItemPb { InstanceId = 1, ItemId = 1012, Count = 1, Equipped = true,
            SlotIndex = 0, Equipment = new EquipmentPb() };
        var data = ItemPbData.FromPb(ItemPb.Parser.ParseFrom(item.ToByteArray()));
        Assert.True(data.Equipped);
        Assert.True(data.HasEquipment);
        Assert.Equal(item, data.ToPb());
        item.SlotIndex = 1;
        Assert.Throws<InvalidDataException>(() => ItemPbData.FromPb(item));
        var summary = new RoleSummaryPb { RoleId = 1001, Name = "展示角色", Level = 1,
            Gender = RoleGenderPb.Male, EquippedItems = { 1012, 1014 } };
        var snapshot = RoleSummaryPbData.FromPb(summary);
        summary.EquippedItems.Clear();
        Assert.Equal([1012, 1014], snapshot.EquippedItems);
        Assert.Equal([1012, 1014], snapshot.ToPb().EquippedItems);
    }

    /// <summary>无私有数据即可转换完整展示，输出和输入均不能修改快照。</summary>
    [Fact]
    public void EquipmentConvertsIndependentlyAndEmptyListClearsAllSlots()
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, EquippedItems = { 1012 } }.WithRevision(2);
        var snapshot = RoleWorldEquipmentPbData.FromPb(message, Definitions);
        Assert.Equal(message, snapshot.ToPb());
        message.EquippedItems.Clear();
        snapshot.ToPb().EquippedItems.Clear();
        Assert.Single(snapshot.EquippedItems);
        var empty = RoleWorldEquipmentPbData.FromPb(new RoleWorldEquipmentPb { RoleId = 1001 }.WithRevision(3), Definitions);
        Assert.Empty(empty.EquippedItems);
        Assert.Equal(3, empty.Revision);
        Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(null!, Definitions));
    }

    /// <summary>role-equipment：配置推导的剑盾合法，其他主手武器排斥副手。</summary>
    [Theory]
    [InlineData(1012)]
    [InlineData(1013)]
    [InlineData(1015)]
    [InlineData(1016)]
    [InlineData(1017)]
    [InlineData(1018)]
    public void PublicSnapshotEnforcesCrossSlotRules(int mainId)
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, EquippedItems = { mainId } };
        Assert.Single(RoleWorldEquipmentPbData.FromPb(message, Definitions).Items);
        message.EquippedItems.Add(1014);
        if (mainId == 1012) Assert.Equal(2, RoleWorldEquipmentPbData.FromPb(message, Definitions).Items.Count);
        else Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(message, Definitions));
    }

    /// <summary>未知配置、重复推导槽位、无效身份及本地非法版本均拒绝。</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    public void InvalidPublicEquipmentIsRejected(string kind)
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, EquippedItems = { 1012 } }.WithRevision(1);
        switch (kind)
        {
            case "identity": message.RoleId = 0; break;
            case "revision": message.WithRevision(-1); break;
            case "duplicate": message.EquippedItems.Add(1017); break;
            case "unknown": message.EquippedItems[0] = 99999; break;
        }
        Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(message, Definitions));
    }
}
