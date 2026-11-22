using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.Utility;

public static class TransformUtility
{
    public static float4x4 GetModelMatrix(TransformComponent transform)
    {
        return float4x4.CreateScale(transform.Scale) *
               float4x4.CreateFromQuaternion(transform.Rotation) *
               float4x4.CreateTranslation(transform.Position);
    }
    
    public static float3 GetScale(float4x4 WorldMatrix)
    {
        var scaleX = new float3(WorldMatrix.X.X, WorldMatrix.X.Y, WorldMatrix.X.Z).Length();
        var scaleY = new float3(WorldMatrix.Y.X, WorldMatrix.Y.Y, WorldMatrix.Y.Z).Length();
        var scaleZ = new float3(WorldMatrix.Z.X, WorldMatrix.Z.Y, WorldMatrix.Z.Z).Length();

        return new float3(scaleX, scaleY, scaleZ);
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

    public static (Single Yaw, Single Pitch) GetYawPitch(floatQuat rotation)
    {
        var forward = rotation * -float3.UnitZ;

        var yaw = Single.Atan2(-forward.X, -forward.Z);
        var pitch = Single.Asin(Single.Clamp(forward.Y, -1.0f, 1.0f));

        return (yaw, pitch);
    }
}