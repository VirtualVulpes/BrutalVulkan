using Brutal.Numerics;
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
}