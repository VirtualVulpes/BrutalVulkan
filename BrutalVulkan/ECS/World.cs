using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.ECS;

public sealed partial class World
{
    private readonly List<UInt32> _generations = [];
    private readonly Stack<EntityId> _freeIds = [];

    private readonly Dictionary<Type, IComponentPool> _componentPools = [];

    public Entity CreateEntity()
    {
        UInt32 generation = 0;

        if (!_freeIds.TryPop(out var id))
        {
            id = new EntityId((UInt32)_generations.Count);
            _generations.Add(0);
        }
        else
        {
            generation = _generations[(Int32)id.Value];
        }

        var entity = new Entity
        {
            Id = id,
            Generation = generation
        };

        return entity;
    }

    private void RemoveEntity(Entity entity)
    {
        if (!IsEntityAlive(entity)) return;

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