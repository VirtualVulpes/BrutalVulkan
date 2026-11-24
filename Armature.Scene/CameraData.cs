using Brutal.Numerics;

namespace Armature.Scene;

public struct CameraData
{
    public float4x4 View;
    public float4x4 Proj;
    public float3 Position;
    public float2 ViewportSize;
}