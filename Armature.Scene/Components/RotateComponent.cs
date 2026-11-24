using Brutal.Numerics;

namespace Armature.Scene.Components;

// velocities should be in radians per second
public struct RotateComponent
{
    public float3 LocalAngularVelocity;
    public float3 WorldAngularVelocity;
}