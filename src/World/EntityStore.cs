using System;
using System.Collections.Generic;

namespace Rpg.World;

/// <summary>以整数实体 ID 保存轻量 ECS 组件。</summary>
internal sealed class EntityStore
{
    /// <summary>实体到组件类型的索引。</summary>
    private readonly Dictionary<int, Dictionary<Type, object>> _entities = new();
    /// <summary>下一次分配的实体 ID。</summary>
    private int _nextId = 1;

    /// <summary>清空当前世界中的所有实体。</summary>
    public void Clear() { _entities.Clear(); _nextId = 1; }

    /// <summary>为一组组件创建实体。</summary>
    public int Create(params object[] components)
    {
        var id = _nextId++;
        var store = new Dictionary<Type, object>();
        foreach (var component in components) store[component.GetType()] = component;
        _entities[id] = store;
        return id;
    }

    /// <summary>读取已存在实体的指定组件。</summary>
    public T Get<T>(int entityId) where T : class => (T)_entities[entityId][typeof(T)];

    /// <summary>为已存在实体添加或替换纯数据组件。</summary>
    public void Set<T>(int entityId, T component) where T : class => _entities[entityId][typeof(T)] = component;

    /// <summary>尝试读取可选组件，不要求所有地图对象都具有生命。</summary>
    public T? Find<T>(int entityId) where T : class
        => _entities[entityId].TryGetValue(typeof(T), out var component) ? (T)component : null;

    /// <summary>移除已离开世界的动态实体。</summary>
    public void Remove(int entityId) => _entities.Remove(entityId);
}
