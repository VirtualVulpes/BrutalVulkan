using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan.Rendering.GpuTypes;

[StructLayout(LayoutKind.Explicit, Size = 288)]
public record struct GpuCamera
{
    [FieldOffset(0)]
    public float4x4 View;
    
    [FieldOffset(64)]
    public float4x4 Proj;
    
    [FieldOffset(128)]
    public float4x4 InvView;
    
    [FieldOffset(192)]
    public float4x4 InvProj;
    
    [FieldOffset(256)]
    public float3 Position;
    
    [FieldOffset(272)]
    public float2 InvViewport;
}