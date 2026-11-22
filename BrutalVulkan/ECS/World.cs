using BrutalVulkan.ECS.Components;
using CommunityToolkit.HighPerformance;

namespace BrutalVulkan.ECS;

public sealed partial class World
{
    private readonly List<UInt32> _generations = [];
    private readonly Stack<EntityId> _freeIds = [];

    private readonly List<Entity> _aliveEntities = [];
    private readonly List<UInt32> _denseIndexBySlot = [];

    private readonly Dictionary<Type, IComponentPool> _componentPools = [];

    public ReadOnlySpan<Entity> AliveEntities => _aliveEntities.AsSpan();

    public Entity CreateEntity()
    {
        UInt32 generation = 1;

        if (!_freeIds.TryPop(out var id))
        {
            id = new EntityId((UInt32)_generations.Count);
            _generations.Add(generation);
            _denseIndexBySlot.Add((UInt32)_aliveEntities.Count);
        }
        else
        {
            generation = _generations[(Int32)id.Value];
            _denseIndexBySlot[(Int32)id.Value] = (UInt32)_aliveEntities.Count;
        }

        var entity = new Entity
        {
            Id = id,
            Generation = generation
        };

        _aliveEntities.Add(entity);

        return entity;
    }

    private void RemoveEntity(Entity entity)
    {
        if (!IsEntityAlive(entity)) return;

        var denseIndex = _denseIndexBySlot[(Int32)entity.Id.Value];
        var lastIndex = _aliveEntities.Count - 1;

        if (denseIndex != lastIndex)
        {
            var moved = _aliveEntities[lastIndex];
            _aliveEntities[(Int32)denseIndex] = moved;
            _denseIndexBySlot[(Int32)moved.Id.Value] = denseIndex;
        }

        _denseIndexBySlot[(Int32)entity.Id.Value] = EntityId.Invalid.Value;
        _aliveEntities.RemoveAt(lastIndex);

        foreach (var pool in _componentPools.Values)
            pool.Remove(entity);

        _generations[(Int32)entity.Id.Value]++;
        _freeIds.Push(entity.Id);
    }

    public void DestroyEntity(Entity entity)
    {
        var toDestroy = new List<Entity>();
        Collect(entity, toDestroy);
        foreach (var e in toDestroy)
            RemoveEntity(e);
    }

    public void DestroyEntities(ReadOnlySpan<Entity> entities)
    {
        var toDestroy = new List<Entity>();

        foreach (var entity in entities)
        {
            Collect(entity, toDestroy);
        }

        foreach (var e in toDestroy)
            RemoveEntity(e);
    }

    private void Collect(Entity e, List<Entity> result)
    {
        result.Add(e);
        foreach (var item in Query<ParentComponent>())
        {
            if (item.C1.Parent == e)
                Collect(item.Entity, result);
        }
    }

    public Boolean IsEntityAlive(Entity entity)
    {
        return entity.Id != EntityId.Invalid
               && entity.Id.Value < _generations.Count
               && _generations[(Int32)entity.Id.Value] == entity.Generation;
    }

    private void ValidateEntity(Entity entity)
    {
        if (!IsEntityAlive(entity))
            throw new InvalidOperationException($"Invalid or destroyed entity {entity}");
    }

    public void SetParent(Entity child, Entity parent)
    {
        AddComponent(child, new ParentComponent { Parent = parent });
    }

    public ComponentPool<T> GetPool<T>() where T : struct
    {
        var type = typeof(T);

        if (!_componentPools.TryGetValue(type, out var pool))
        {
            pool = new ComponentPool<T>();
            _componentPools.Add(type, pool);
        }

        return (ComponentPool<T>)pool;
    }

    public void AddComponent<T>(Entity entity, T component) where T : struct
    {
        ValidateEntity(entity);
        GetPool<T>().Add(entity, component);
    }

    public Boolean HasComponent<T>(Entity entity) where T : struct
    {
        ValidateEntity(entity);
        return GetPool<T>().Has(entity);
    }

    public ref T GetComponent<T>(Entity entity) where T : struct
    {
        ValidateEntity(entity);
        return ref GetPool<T>().Get(entity);
    }

    public Boolean TryRemoveComponent<T>(Entity entity) where T : struct
    {
        ValidateEntity(entity);
        return GetPool<T>().Remove(entity);
    }
}