using Brutal.Numerics;

namespace BrutalVulkan.ECS.Components;

public struct TransformComponent
{
    public float3 Position;
    public floatQuat Rotation;
    public float3 Scale;

    public static TransformComponent Identity => new()
    {
        Position = float3.Zero,
        Rotation = floatQuat.Identity,
        Scale = float3.One
    };
}