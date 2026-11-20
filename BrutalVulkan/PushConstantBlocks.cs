using System.Runtime.InteropServices;
using Brutal.Numerics;

namespace BrutalVulkan;

public static class PushConstantBlocks
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ImGuiBlock
    {
        [FieldOffset(0)]
        public float2 Scale;
        
        [FieldOffset(8)]
        public float2 Translate;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VertexBlock
    {
        [FieldOffset(0)]
        public float4x4 Mvp;
        
        [FieldOffset(64)]
        public float4x4 Model;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TextureFragmentBlock
    {
        [FieldOffset(0)]
        public float3 CameraPosition;
        
        [FieldOffset(12)]
        public UInt32 TextureId;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    public struct ColorFragmentBlock
    {
        [FieldOffset(0)]
        public float3 CameraPosition;
        
        [FieldOffset(16)]
        public float3 Color;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct JumpPassBlock
    {
        [FieldOffset(0)]
        public UInt32 JumpDistance;
    }
        
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct EntityIdBlock
    {
        [FieldOffset(0)]
        public UInt32 Id;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AtmosphereBlock
    {
        [FieldOffset(0)]
        public float3 Center;

        [FieldOffset(12)] 
        public Single Radius;
        
        [FieldOffset(16)] 
        public float3 LightPosition;
    }
}