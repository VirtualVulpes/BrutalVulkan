using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan;

public class PushConstantBlocks
{
    public struct ImGui
    {
        public float2 Scale;
        public float2 Translate;
    }

    public struct Vertex
    {
        public float4x4 Mvp;
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