using System;
using System.Collections.Generic;
using Godot;

/// <summary>管理 ECS 实体、组件和系统的生命周期与调度。</summary>
[GlobalClass]
public partial class EcsWorld : RefCounted
{
    /// <summary>分配给下一个实体的标识，销毁后不复用。</summary>
    private long _nextEntityId = 1;

    /// <summary>记录当前有效的实体标识。</summary>
    private readonly HashSet<long> _entities = [];

    /// <summary>按实体标识和组件运行时类型保存组件。</summary>
    private readonly Dictionary<long, Dictionary<Type, EcsComponent>> _components = [];

    /// <summary>保存系统的注册顺序。</summary>
    private readonly List<EcsSystem> _systems = [];

    /// <summary>记录系统注册令牌，区分更新期间的重新注册。</summary>
    private readonly Dictionary<EcsSystem, long> _systemTokens = new(ReferenceEqualityComparer.Instance);

    /// <summary>分配给下一次系统注册的令牌。</summary>
    private long _nextSystemToken = 1;

    /// <summary>创建实体并返回不会复用的标识。</summary>
    public long CreateEntity()
    {
        var entityId = _nextEntityId++;
        _entities.Add(entityId);
        _components.Add(entityId, []);
        return entityId;
    }

    /// <summary>判断实体标识当前是否有效。</summary>
    public bool HasEntity(long entityId) => _entities.Contains(entityId);

    /// <summary>销毁实体及其组件；标识无效时返回 false。</summary>
    public bool DestroyEntity(long entityId)
    {
        if (!_entities.Remove(entityId))
            return false;
        _components.Remove(entityId);
        return true;
    }

    /// <summary>按组件的精确运行时类型附加或替换实体组件。</summary>
    public bool AddComponent(long entityId, EcsComponent? component)
    {
        if (!HasEntity(entityId) || component is null)
            return false;
        _components[entityId][component.GetType()] = component;
        return true;
    }

    /// <summary>判断有效实体是否具有指定精确类型的组件。</summary>
    public bool HasComponent(long entityId, Type? componentType) =>
        componentType is not null && HasEntity(entityId) && _components[entityId].ContainsKey(componentType);

    /// <summary>判断有效实体是否具有指定泛型类型的组件。</summary>
    public bool HasComponent<T>(long entityId) where T : EcsComponent => HasComponent(entityId, typeof(T));

    /// <summary>读取组件；实体或类型不存在时返回空值。</summary>
    public EcsComponent? GetComponent(long entityId, Type? componentType) =>
        HasComponent(entityId, componentType) ? _components[entityId][componentType!] : null;

    /// <summary>按静态类型读取组件，缺失时返回空值。</summary>
    public T? GetComponent<T>(long entityId) where T : EcsComponent =>
        GetComponent(entityId, typeof(T)) as T;

    /// <summary>移除指定精确类型组件，并报告此前是否存在。</summary>
    public bool RemoveComponent(long entityId, Type? componentType) =>
        HasComponent(entityId, componentType) && _components[entityId].Remove(componentType!);

    /// <summary>按泛型类型移除组件，并报告此前是否存在。</summary>
    public bool RemoveComponent<T>(long entityId) where T : EcsComponent =>
        RemoveComponent(entityId, typeof(T));

    /// <summary>返回具有全部指定组件的实体标识快照，按创建顺序排列。</summary>
    public long[] Query(params Type[] componentTypes)
    {
        ArgumentNullException.ThrowIfNull(componentTypes);
        var matches = new List<long>();
        foreach (var entityId in _entities)
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
    public bool AddSystem(EcsSystem? system)
    {
        if (system is null || _systemTokens.ContainsKey(system))
            return false;
        _systems.Add(system);
        _systemTokens.Add(system, _nextSystemToken++);
        return true;
    }

    /// <summary>注销系统，并报告它此前是否已注册。</summary>
    public bool RemoveSystem(EcsSystem? system)
    {
        if (system is null || !_systemTokens.Remove(system))
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
