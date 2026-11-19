using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Utility;

namespace BrutalVulkan.ECS.Systems;

public class RotateSystem : ISystem
{
    private World _world;

    public RotateSystem(World world)
    {
        _world = world;
    }
    
    public void Start()
    {
    }

    public void Update(Single deltaTime)
    {
        var query = _world.Query<TransformComponent, RotateComponent>();

        foreach (var item in query)
        {
            if (item.C2.LocalAngularVelocity != float3.Zero)
            {
                var speed = item.C2.LocalAngularVelocity.Length();
                var axis = item.C2.LocalAngularVelocity / speed;

                TransformUtility.RotateLocal(ref item.C1, axis, speed * deltaTime);
            }

            if (item.C2.WorldAngularVelocity != float3.Zero)
            {
                var speed = item.C2.WorldAngularVelocity.Length();
                var axis = item.C2.WorldAngularVelocity / speed;

                TransformUtility.RotateWorld(ref item.C1, axis, speed * deltaTime);
            }
        }
    }
}