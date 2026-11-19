using Brutal.Numerics;

namespace BrutalVulkan.ECS.Components;

public enum LightType
{
    Point,
    Directional,
    Spot
}

public record struct LightComponent
{
    public float3 Color;
    public Single Intensity;
    public Single Range;
    public float3 Direction;
    public LightType Type;
    public Single InnerCos;
    public Single OuterCos;
}
