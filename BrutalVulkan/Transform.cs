using Brutal.Numerics;

namespace BrutalVulkan;

public struct Transform
{
    public float3 Position;
    public floatQuat Rotation;
    public float3 Scale;

    public static Transform Identity => new()
    {
        Position = float3.Zero,
        Rotation = floatQuat.Identity,
        Scale = float3.One
    };

    public void RotateLocal(float3 axis, float angle)
    {
        Rotation = floatQuat.Normalize(Rotation * floatQuat.CreateFromAxisAngle(axis, angle));
    }

    public void RotateWorld(float3 axis, float angle)
    {
        Rotation = floatQuat.Normalize(floatQuat.CreateFromAxisAngle(axis, angle) * Rotation);
    }

    public readonly float4x4 GetModelMatrix()
    {
        return float4x4.CreateScale(Scale) *
               float4x4.CreateFromQuaternion(Rotation) *
               float4x4.CreateTranslation(Position);
    }
}