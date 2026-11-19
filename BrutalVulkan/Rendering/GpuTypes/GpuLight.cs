using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan.Rendering.GpuTypes;

[StructLayout(LayoutKind.Explicit, Size = 64)]
public record struct GpuLight
{
    [FieldOffset(0)]
    public float3 Position;
    
    [FieldOffset(12)]
    public Single Intensity;
    
    [FieldOffset(16)]
    public float3 Color;
    
    [FieldOffset(28)]
    public Single Range;
    
    [FieldOffset(32)]
    public float3 Direction;
    
    [FieldOffset(44)]
    public Int32 Type;
    
    [FieldOffset(48)]
    public Single InnerCos;
    
    [FieldOffset(52)]
    public Single OuterCos;
}