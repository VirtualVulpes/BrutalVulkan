using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan.Rendering.GpuTypes;

[StructLayout(LayoutKind.Explicit, Size = 32)]
public record struct GpuLight
{
    [FieldOffset(0)]
    public float3 Position;
    
    [FieldOffset(16)]
    public float3 Color;
    
    [FieldOffset(28)]
    public Single Intensity;
}