using Brutal.GlfwApi;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Input;
using CommunityToolkit.HighPerformance;

namespace BrutalVulkan.UI;

public sealed class SelectionHelper
{
    private readonly World _world;
    private readonly InputManager _input;
    
    private Int32 _nextSelected = 1;

    public SelectionHelper(World world, InputManager input)
    {
        _world = world;
        _input = input;
    }

    public void SelectEntity(Entity e)
    {
        if (_input.IsKeyDown(GlfwKey.LeftShift))
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
            
            if (_world.IsEntityAlive(e) && !_world.HasComponent<SelectionComponent>(e))
                AddSelection(e);
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