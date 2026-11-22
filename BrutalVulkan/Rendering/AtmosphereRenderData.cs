using Brutal.Numerics;

namespace BrutalVulkan.Rendering;

public struct AtmosphereRenderData
{
    public float3 Center;
    public Single Radius;
    public float3 LightDirection;
    public UInt32 Enabled;
    public float3 LightColor;
    public Single LightIntensity;
}