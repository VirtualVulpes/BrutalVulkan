using Armature.Scene;
using Armature.Scene.Components;
using CommunityToolkit.HighPerformance;

namespace BrutalVulkan.UI;

public sealed class SelectionHelper
{
    private readonly World _world;
    
    private Int32 _nextSelected = 1;

    public SelectionHelper(World world)
    {
        _world = world;
    }

    private void SelectEntity(Entity e, Boolean additive)
    {
        if (additive)
        {
            if (!_world.IsEntityAlive(e)) return;
            
            if (!_world.TryRemoveComponent<SelectionComponent>(e))
            {
                AddSelection(e);
            }
        }
        else
        {
            foreach (var item in _world.Query<SelectionComponent>())
            {
                _world.TryRemoveComponent<SelectionComponent>(item.Entity);
            }
            
            if (_world.IsEntityAlive(e))
                AddSelection(e);
        }

    }

    public void Click(Entity? entity, Boolean additive)
    {
        if (entity is { } e) SelectEntity(e, additive);
        else if (!additive) ClearSelection();
    }

    public void ClearSelection()
    {
        foreach (var item in _world.Query<SelectionComponent>())
        {
            _world.TryRemoveComponent<SelectionComponent>(item.Entity);
        }
    }

    private void AddSelection(Entity e)
    {
        var order = _nextSelected++;
        _world.AddComponent(e, new SelectionComponent{Order = order});
    }

    public Entity GetActive()
    {
        var e = Entity.Invalid;
        var order = -1;
        
        foreach (var item in _world.Query<SelectionComponent>())
        {
            if (item.C1.Order > order)
            {
                e = item.Entity;
                order = item.C1.Order;
            }
        }

        return e;
    }
    
    public ReadOnlySpan<Entity> GetSelected()
    {
        var entities = new List<Entity>();
        
        foreach (var item in _world.Query<SelectionComponent>())
        {
            entities.Add(item.Entity);
        }

        return entities.AsSpan();
    }
}