using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan;

public static class PushConstantBlocks
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ImGui
    {
        [FieldOffset(0)]
        public float2 Scale;
        
        [FieldOffset(8)]
        public float2 Translate;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct Vertex
    {
        [FieldOffset(0)]
        public float4x4 Mvp;
        
        [FieldOffset(64)]
        public float4x4 Model;
    }

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct Fragment
    {
        [FieldOffset(0)]
        public UInt32 TextureId;
        
        [FieldOffset(16)]
        public float3 CameraPosition;
    }
}