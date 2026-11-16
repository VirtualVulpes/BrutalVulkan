using Brutal.Numerics;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.ECS.Systems;

public class RotateSystem : ISystem
{
    private World _world;

    public RotateSystem(World world)
    {
        _world = world;
    }
    
    private static void RotateLocal(ref TransformComponent transform, float3 axis, Single angle)
    {
        transform.Rotation = floatQuat.Normalize(transform.Rotation * floatQuat.CreateFromAxisAngle(axis, angle));
    }

    private static void RotateWorld(ref TransformComponent transform, float3 axis, Single angle)
    {
        transform.Rotation = floatQuat.Normalize(floatQuat.CreateFromAxisAngle(axis, angle) * transform.Rotation);
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
            
                RotateLocal(ref item.C1, axis, speed * deltaTime);
            }

            if (item.C2.WorldAngularVelocity != float3.Zero)
            {
                var speed = item.C2.WorldAngularVelocity.Length();
                var axis = item.C2.WorldAngularVelocity / speed;
            
                RotateWorld(ref item.C1, axis, speed * deltaTime);
            }
        }
    }
}