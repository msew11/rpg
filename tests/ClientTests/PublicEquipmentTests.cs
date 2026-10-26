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

    /// <summary>公开投影排除背包字段，完整和独立消息都能独立序列化。</summary>
    [Fact]
    public void PublicProjectionAndWireMessagesContainOnlyEquipment()
    {
        var role = new DataServerRole { RoleId = 1001, Bag = new DataServerBag
        {
            Revision = 12, EquipmentRevision = 3, NextInstanceId = 3,
            Items = [new DataServerItem(2, 1012, 17, 1)],
            Equipment = [new DataServerEquipment(1, 1012, EquipmentSlot.MainHand)]
        } };
        var notice = MockServer.CreateEquipmentNotice(role);
        var decoded = Assert.IsType<RoleEquipmentNotice>(MessageCodec.ReadPayload(
            MessageCodec.ReadEnvelope(MessageCodec.Encode(notice, 7, false)), false));
        Assert.Equal(notice, decoded);
        Assert.NotSame(notice.Equipment, decoded.Equipment);
        Assert.Equal(3, decoded.Equipment.Revision);
        Assert.Equal(1, Assert.Single(decoded.Equipment.Items).InstanceId);
        Assert.Equal(["role_id", "revision", "items"], RoleWorldEquipmentPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        Assert.Equal(["instance_id", "item_id", "slot"], EquippedItemPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        Assert.Equal(["revision", "items"], BagPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        Assert.Equal(RoleWorldEquipmentPb.Descriptor, RoleWorldStatePb.Descriptor.FindFieldByNumber(4).MessageType);
        Assert.Null(RoleWorldProfilePb.Descriptor.FindFieldByName("equipment"));
        Assert.Contains("equipment", BagPb.Descriptor.ToProto().ReservedName);
        Assert.Contains(BagPb.Descriptor.ToProto().ReservedRange, range => range.Start == 3 && range.End == 4);
        Assert.Equal(4, EquipItem.Descriptor.FindFieldByName("expected_bag_revision").FieldNumber);
        Assert.Equal(6, EquipItem.Descriptor.FindFieldByName("expected_equipment_revision").FieldNumber);
        Assert.Throws<InvalidDataException>(() => MessageCodec.Encode(notice, 7, true));
        Assert.Throws<InvalidDataException>(() => MessageCodec.ReadPayload(new EnvelopePb
            { Generation = 7, MessageType = "rpg.BagChangedNotice" }, false));

        var items = new RoleItemsChangedNotice { RoleId = role.RoleId,
            Bag = new BagPb { Revision = role.Bag.Revision }, Equipment = notice.Equipment };
        Assert.Equal(items, MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(MessageCodec.Encode(items, 7, false)), false));
        Assert.Throws<InvalidDataException>(() => MessageCodec.Encode(items, 7, true));
        var equip = new EquipItem { RoleId = 1001, InstanceId = 2, Slot = EquipmentSlotPb.MainHand,
            ExpectedBagRevision = 12, ExpectedEquipmentRevision = 3 };
        Assert.Equal(equip, MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(MessageCodec.Encode(equip, 7, true)), true));
        Assert.Throws<InvalidDataException>(() => MessageCodec.Encode(equip, 7, false));
    }

    /// <summary>无私有数据即可转换全量装备，原协议和输出协议均不能修改缓存。</summary>
    [Fact]
    public void EquipmentConvertsIndependentlyAndEmptyListClearsAllSlots()
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 2,
            Items = { new EquippedItemPb { InstanceId = 1, ItemId = 1012, Slot = EquipmentSlotPb.MainHand } } };
        var snapshot = RoleWorldEquipmentPbData.FromPb(message, Definitions);
        Assert.Equal(message, snapshot.ToPb());
        message.Items.Clear();
        snapshot.ToPb().Items.Clear();
        Assert.Single(snapshot.Items);
        var empty = RoleWorldEquipmentPbData.FromPb(new RoleWorldEquipmentPb { RoleId = 1001, Revision = 3 }, Definitions);
        Assert.Empty(empty.Items);
        Assert.Equal(3, empty.Revision);
        Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(null!, Definitions));
    }

    /// <summary>role-equipment：普通剑盾可共存，全部双手武器只允许单件主手公开实例。</summary>
    [Theory]
    [InlineData(1012)]
    [InlineData(1013)]
    [InlineData(1015)]
    [InlineData(1016)]
    [InlineData(1017)]
    [InlineData(1018)]
    public void PublicSnapshotEnforcesCrossSlotRules(int mainId)
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 1,
            Items = { new EquippedItemPb { InstanceId = 1, ItemId = mainId, Slot = EquipmentSlotPb.MainHand } } };
        Assert.Single(RoleWorldEquipmentPbData.FromPb(message, Definitions).Items);
        message.Items.Add(new EquippedItemPb { InstanceId = 2, ItemId = 1014, Slot = EquipmentSlotPb.OffHand });
        if (mainId == 1012) Assert.Equal(2, RoleWorldEquipmentPbData.FromPb(message, Definitions).Items.Count);
        else Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(message, Definitions));
        message.Items.RemoveAt(0);
        Assert.Single(RoleWorldEquipmentPbData.FromPb(message, Definitions).Items);
        message.Items[0].ItemId = mainId;
        Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(message, Definitions));
    }

    /// <summary>非法身份、版本、重复实例或槽位均不能形成可消费的公开装备。</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("instance")]
    [InlineData("slot")]
    [InlineData("category")]
    public void InvalidPublicEquipmentIsRejected(string kind)
    {
        var message = new RoleWorldEquipmentPb { RoleId = 1001, Revision = 1,
            Items = { new EquippedItemPb { InstanceId = 1, ItemId = 1012, Slot = EquipmentSlotPb.MainHand } } };
        switch (kind)
        {
            case "identity": message.RoleId = 0; break;
            case "revision": message.Revision = -1; break;
            case "instance": message.Items.Add(message.Items[0].Clone()); break;
            case "slot": message.Items.Add(new EquippedItemPb { InstanceId = 2, ItemId = 1012, Slot = EquipmentSlotPb.MainHand }); break;
            case "category": message.Items[0].Slot = EquipmentSlotPb.OffHand; break;
        }
        Assert.Throws<InvalidDataException>(() => RoleWorldEquipmentPbData.FromPb(message, Definitions));
    }
}
