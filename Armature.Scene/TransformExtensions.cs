using Armature.Scene.Components;
using Brutal.Numerics;

namespace Armature.Scene;

public static class TransformExtensions
{
    public static float4x4 GetWorldMatrix(World world, Entity entity)
    {
        var transform = world.GetComponent<TransformComponent>(entity);
        var localMatrix = GetModelMatrix(transform);

        if (!world.HasComponent<ParentComponent>(entity))
            return localMatrix;

        var parent = world.GetComponent<ParentComponent>(entity).Parent;
        return localMatrix * GetWorldMatrix(world, parent);
    }
    
    public static float4x4 GetModelMatrix(TransformComponent transform)
    {
        return float4x4.CreateScale(transform.Scale) *
               float4x4.CreateFromQuaternion(transform.Rotation) *
               float4x4.CreateTranslation(transform.Position);
    }

    public static void MoveLocal(ref TransformComponent transform, float3 delta)
    {
        transform.Position += Right(transform) * delta.X + float3.UnitY * delta.Y + Forward(transform) * delta.Z;
    }

    public static void RotateLocal(ref TransformComponent transform, float3 axis, Single angle)
    {
        transform.Rotation = floatQuat.Normalize(transform.Rotation * floatQuat.CreateFromAxisAngle(axis, angle));
    }

    public static void RotateWorld(ref TransformComponent transform, float3 axis, Single angle)
    {
        transform.Rotation = floatQuat.Normalize(floatQuat.CreateFromAxisAngle(axis, angle) * transform.Rotation);
    }

    public static float3 Forward(TransformComponent transform)
    {
        return transform.Rotation * -float3.UnitZ;
    }

    public static float3 Right(TransformComponent transform)
    {
        return transform.Rotation * float3.UnitX;
    }
}