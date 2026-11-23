using Brutal.Numerics;

namespace BrutalVulkan.ECS.Components;

public record struct CelestialBodyComponent
{
    public double3 PositionRelParent;
    public double3 VelocityRelParent;
    public doubleQuat Rotation;
}