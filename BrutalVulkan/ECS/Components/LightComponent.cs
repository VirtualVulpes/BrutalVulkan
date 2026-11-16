using Brutal.Numerics;

namespace BrutalVulkan.ECS.Components;

public record struct LightComponent
{
    public float3 Color;
    public Single Intensity;
}
