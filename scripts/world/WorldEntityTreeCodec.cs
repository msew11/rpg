using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>编解码区块实体树的持久组件与父子关系。</summary>
internal static class WorldEntityTreeCodec
{
    /// <summary>记录一个组件的稳定类型名和落地内容。</summary>
    private sealed class PersistedComponentRecord
    {
        public string Type { get; set; } = "";
        public string Data { get; set; } = "";
    }

    /// <summary>记录区块下一个实体的组件和子实体。</summary>
    private sealed class PersistedEntityRecord
    {
        public List<PersistedComponentRecord> Components { get; set; } = [];
        public List<PersistedEntityRecord> Children { get; set; } = [];
    }

    /// <summary>序列化区块实体及其全部子树。</summary>
    internal static string Capture(EcsWorld ecs, long entityId)
    {
        var record = CaptureEntity(ecs, entityId);
        return record.Components.Count == 0 && record.Children.Count == 0
            ? "{}" : JsonSerializer.Serialize(record);
    }

    /// <summary>递归收集一个实体及其可落地组件。</summary>
    private static PersistedEntityRecord CaptureEntity(EcsWorld ecs, long entityId)
    {
        var result = new PersistedEntityRecord();
        foreach (var (type, component) in ecs.Data.Components[entityId])
        {
            var data = component.ToPersistedString();
            if (data is not null)
                result.Components.Add(new PersistedComponentRecord { Type = type.FullName!, Data = data });
        }
        foreach (var childId in ecs.GetChildren(entityId))
            result.Children.Add(CaptureEntity(ecs, childId));
        return result;
    }

    /// <summary>根据临时存档重建区块实体及其子树状态。</summary>
    internal static void Restore(EcsWorld ecs, long chunkId, string json)
    {
        var record = JsonSerializer.Deserialize<PersistedEntityRecord>(json);
        if (record is not null)
            RestoreEntityData(ecs, chunkId, record);
    }

    /// <summary>在读档前验证实体子树及其组件落地字符串可还原。</summary>
    internal static bool CanRestore(string json)
    {
        try
        {
            var record = JsonSerializer.Deserialize<PersistedEntityRecord>(json);
            var remaining = 10000;
            if (record is not null)
                ValidateEntity(record, 0, ref remaining);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>限制坏档的实体树深度并检查组件类型与数据。</summary>
    private static void ValidateEntity(PersistedEntityRecord record, int depth, ref int remaining)
    {
        if (depth > 64 || --remaining < 0 || record.Components is null || record.Children is null)
            throw new FormatException("Invalid persisted entity tree.");
        foreach (var entry in record.Components)
        {
            var type = ResolveComponentType(entry.Type);
            if (type is null || !typeof(EcsComponent).IsAssignableFrom(type) ||
                Activator.CreateInstance(type) is not EcsComponent component)
                throw new FormatException("Invalid persisted component type.");
            component.FromPersistedString(entry.Data);
            component.Dispose();
        }
        foreach (var child in record.Children)
            ValidateEntity(child, depth + 1, ref remaining);
    }

    /// <summary>仅从当前游戏程序集解析落地组件类型。</summary>
    private static Type? ResolveComponentType(string name) => typeof(WorldEntityTreeCodec).Assembly.GetType(name);

    /// <summary>恢复一个子实体的组件及其后代。</summary>
    private static void RestoreEntity(EcsWorld ecs, long parentId, PersistedEntityRecord record)
    {
        var entityId = ecs.CreateEntity(parentId);
        RestoreEntityData(ecs, entityId, record);
    }

    /// <summary>恢复已创建实体的组件并递归创建其后代。</summary>
    private static void RestoreEntityData(EcsWorld ecs, long entityId, PersistedEntityRecord record)
    {
        foreach (var entry in record.Components)
        {
            var type = ResolveComponentType(entry.Type);
            if (type is null || !typeof(EcsComponent).IsAssignableFrom(type) ||
                Activator.CreateInstance(type) is not EcsComponent component)
                throw new FormatException($"Invalid persisted component type: {entry.Type}");
            component.FromPersistedString(entry.Data);
            ecs.AddComponent(entityId, component);
        }
        foreach (var child in record.Children)
            RestoreEntity(ecs, entityId, child);
    }
}
