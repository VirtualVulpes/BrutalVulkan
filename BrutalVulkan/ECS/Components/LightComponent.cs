using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan.ECS.Components;

public record struct LightComponent(float3 Color, Single Intensity);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public record struct GpuLight
{
    public float4 Position;
    public float4 Color;
    public Single Intensity;
    public float3 Padding;
};
