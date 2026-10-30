using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Runtime;
using Rpg.Utils;

namespace Rpg.Server.Modules.Role.Async;

/// <summary>投影已加载或首次保存的角色，按权威位置向地图获取初始范围。</summary>
public sealed class PrepareRoleHandler : IActorHandler<RoleContext, PrepareRole, PrepareRoleRt>
{
    /// <summary>业务入口检查存档可展示性，不改变仓库的原始读写契约。</summary>
    public async ValueTask<PrepareRoleRt> HandleAsync(RoleContext context, PrepareRole request)
    {
        var cache = context.Cache;
        var data = cache.Role.Value;
        if (data.RoleId != cache.RoleId || data.IsDeleted || string.IsNullOrWhiteSpace(data.Name) || data.Level <= 0 ||
            data.Gender is not (Common.Data.RoleGender.Male or Common.Data.RoleGender.Female) || data.HeadId is < 1 or > 74 ||
            data.MaxHealth <= 0 || data.Health < 0 || data.Health > data.MaxHealth || data.MaxMana < 0 || data.Mana < 0 || data.Mana > data.MaxMana ||
            data.Experience < 0 || data.NextExperience <= 0 || data.BagExtraRows < 0 || data.NextInstanceId <= 0 ||
            !context.Config.Map.Items.TryGetValue(data.MapId, out var map) ||
            !float.IsFinite(data.X) || !float.IsFinite(data.Y) || data.X < 0 || data.Y < 0 ||
            data.X >= (long)map.Width * map.TileSize || data.Y >= (long)map.Height * map.TileSize)
            throw new InvalidDataException("角色存档无法用于世界初始展示");
        var capacity = context.Config.Basic.Value.GetBagCapacity(data.BagExtraRows);
        var bag = new BagPb();
        var ids = new HashSet<long>();
        var slots = new HashSet<int>();
        var equippedSlots = new HashSet<Rpg.Const.EquipmentSlot>();
        var extensions = cache.Equipments.Value;
        if (extensions.Any(ext => ext is null) || extensions.Select(ext => ext.InstanceId).Distinct().Count() != extensions.Length)
            throw new InvalidDataException("装备扩展重复或缺失");
        var extensionIds = extensions.Select(ext => ext.InstanceId).ToHashSet();
        var equipped = new RoleWorldEquipmentPb();
        var bothHands = false;
        foreach (var item in cache.Items.Value)
        {
            if (item is null || item.InstanceId <= 0 || item.InstanceId >= data.NextInstanceId || !ids.Add(item.InstanceId) ||
                !context.Config.Item.Items.TryGetValue(item.ItemId, out var cfg) || item.Count <= 0 || item.Count > cfg.MaxStack ||
                item.SlotIndex < 0 || !item.Equipped && (item.SlotIndex >= capacity || !slots.Add(item.SlotIndex)))
                throw new InvalidDataException("物品实例无效");
            var isEquipment = EquipmentSlotUtil.TryGetSlot(cfg.Category, cfg.SubCategory, out var slot);
            if (isEquipment != extensionIds.Contains(item.InstanceId)) throw new InvalidDataException("物品与装备扩展不一致");
            if (item.Equipped)
            {
                if (!isEquipment || cfg.MaxStack != 1 || item.Count != 1 || item.SlotIndex != 0 || item.EquipmentSlot != slot || !equippedSlots.Add(slot))
                    throw new InvalidDataException("已穿戴实例或槽位无效");
                bothHands |= EquipmentSlotUtil.OccupiesBothHands(cfg.Category, cfg.SubCategory);
                equipped.EquippedItems.Add(item.ItemId);
            }
            var message = new ItemPb { InstanceId = item.InstanceId, ItemId = item.ItemId, SlotIndex = item.SlotIndex, Count = item.Count, Equipped = item.Equipped };
            if (isEquipment) message.Equipment = new EquipmentPb();
            bag.Items.Add(message);
        }
        if (extensionIds.Except(ids).Any() || bothHands && equippedSlots.Contains(Rpg.Const.EquipmentSlot.OffHand))
            throw new InvalidDataException("装备扩展孤立或双手规则冲突");
        var state = new RoleWorldStatePb { RoleId = data.RoleId,
            Profile = new() { Name = data.Name, Level = data.Level, Gender = (RoleGenderPb)data.Gender, HeadId = data.HeadId },
            Status = new() { Health = data.Health, MaxHealth = data.MaxHealth, Mana = data.Mana, MaxMana = data.MaxMana },
            Position = new() { MapId = data.MapId, X = data.X, Y = data.Y }, Equipment = equipped };
        var world = await context.Actors.World(data.MapId).Ask<GetWorldEntrySnapshotRt>(new GetWorldEntrySnapshot { X = data.X, Y = data.Y }, context.Call).ConfigureAwait(false);
        return new PrepareRoleRt { State = state, Role = new() { Experience = data.Experience, NextExperience = data.NextExperience, BagExtraRows = data.BagExtraRows }, Bag = bag, Map = world.Map };
    }
}
