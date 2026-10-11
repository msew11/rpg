using System;
using System.Collections.Generic;
using Godot;

/// <summary>管理 ECS 实体、组件和系统的生命周期与调度。</summary>
[GlobalClass]
public partial class EcsWorld : RefCounted
{
    /// <summary>保存实体与组件的单一数据实例。</summary>
    public DataEcsWorld Data { get; }

    /// <summary>创建使用独立实体数据的世界。</summary>
    public EcsWorld() : this(new DataEcsWorld()) { }

    /// <summary>创建使用指定实体数据的世界。</summary>
    public EcsWorld(DataEcsWorld data) => Data = data ?? throw new ArgumentNullException(nameof(data));

    /// <summary>保存系统的注册顺序。</summary>
    private readonly List<EcsSystem> _systems = [];

    /// <summary>记录系统注册令牌，区分更新期间的重新注册。</summary>
    private readonly Dictionary<EcsSystem, long> _systemTokens = new(ReferenceEqualityComparer.Instance);

    /// <summary>分配给下一次系统注册的令牌。</summary>
    private long _nextSystemToken = 1;

    /// <summary>创建实体并返回不会复用的标识。</summary>
    public long CreateEntity()
    {
        var entityId = Data.NextEntityId++;
        Data.Entities.Add(entityId);
        Data.Components.Add(entityId, []);
        return entityId;
    }

    /// <summary>判断实体标识当前是否有效。</summary>
    public bool HasEntity(long entityId) => Data.Entities.Contains(entityId);

    /// <summary>销毁实体及其组件；标识无效时返回 false。</summary>
    public bool DestroyEntity(long entityId)
    {
        if (!Data.Entities.Remove(entityId))
            return false;
        Data.Components.Remove(entityId);
        return true;
    }

    /// <summary>按组件的精确运行时类型附加或替换实体组件。</summary>
    public bool AddComponent(long entityId, EcsComponent component)
    {
        if (!HasEntity(entityId))
            return false;
        Data.Components[entityId][component.GetType()] = component;
        return true;
    }

    /// <summary>判断有效实体是否具有指定精确类型的组件。</summary>
    public bool HasComponent(long entityId, Type componentType) =>
        HasEntity(entityId) && Data.Components[entityId].ContainsKey(componentType);

    /// <summary>判断有效实体是否具有指定泛型类型的组件。</summary>
    public bool HasComponent<T>(long entityId) where T : EcsComponent => HasComponent(entityId, typeof(T));

    /// <summary>读取组件；实体或组件不存在时返回空值。</summary>
    public EcsComponent? GetComponent(long entityId, Type componentType) =>
        HasComponent(entityId, componentType) ? Data.Components[entityId][componentType] : null;

    /// <summary>按静态类型读取组件，缺失时返回空值。</summary>
    public T? GetComponent<T>(long entityId) where T : EcsComponent =>
        GetComponent(entityId, typeof(T)) as T;

    /// <summary>移除指定精确类型组件，并报告此前是否存在。</summary>
    public bool RemoveComponent(long entityId, Type componentType) =>
        HasComponent(entityId, componentType) && Data.Components[entityId].Remove(componentType);

    /// <summary>按泛型类型移除组件，并报告此前是否存在。</summary>
    public bool RemoveComponent<T>(long entityId) where T : EcsComponent =>
        RemoveComponent(entityId, typeof(T));

    /// <summary>返回具有全部指定组件的实体标识快照，按创建顺序排列。</summary>
    public long[] Query(params Type[] componentTypes)
    {
        var matches = new List<long>();
        foreach (var entityId in Data.Entities)
        {
            var containsAll = true;
            foreach (var componentType in componentTypes)
            {
                if (HasComponent(entityId, componentType))
                    continue;
                containsAll = false;
                break;
            }
            if (containsAll)
                matches.Add(entityId);
        }
        matches.Sort();
        return matches.ToArray();
    }

    /// <summary>注册系统；同一实例不能重复注册。</summary>
    public bool AddSystem(EcsSystem system)
    {
        if (_systemTokens.ContainsKey(system))
            return false;
        _systems.Add(system);
        _systemTokens.Add(system, _nextSystemToken++);
        return true;
    }

    /// <summary>注销系统，并报告它此前是否已注册。</summary>
    public bool RemoveSystem(EcsSystem system)
    {
        if (!_systemTokens.Remove(system))
            return false;
        _systems.RemoveAll(candidate => ReferenceEquals(candidate, system));
        return true;
    }

    /// <summary>按注册顺序更新系统，执行期间新增或重新注册的系统留到下次更新。</summary>
    public void Step(double delta)
    {
        var scheduled = new List<(EcsSystem System, long Token)>();
        foreach (var system in _systems)
            scheduled.Add((system, _systemTokens[system]));
        foreach (var (system, token) in scheduled)
        {
            if (_systemTokens.TryGetValue(system, out var currentToken) && currentToken == token)
                system.Update(this, delta);
        }
    }
}
