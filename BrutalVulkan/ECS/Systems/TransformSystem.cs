using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Utility;

namespace BrutalVulkan.ECS.Systems;

public sealed class TransformSystem : ISystem
{
    private World _world;
    
    public TransformSystem(World world)
    {
        _world = world;
    }

    public float4x4 GetWorldMatrix(Entity e)
    {
        var transform = _world.GetComponent<TransformComponent>(e);
        var localMatrix = TransformUtility.GetModelMatrix(transform);

        if (!_world.HasComponent<ParentComponent>(e))
            return localMatrix;

        var parent = _world.GetComponent<ParentComponent>(e).Parent;
        return localMatrix * GetWorldMatrix(parent);
    }
    
    public void Start()
    {
    }

    public void Update(Single deltaTime)
    {
    }
}