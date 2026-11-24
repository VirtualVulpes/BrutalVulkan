using Armature.Core;
using Armature.Vulkan;
using Brutal.Numerics;

namespace Armature.Rendering;

public readonly struct RenderItem
{
    public float4x4 ModelMatrix { get; init; }

    public GpuMesh GpuMesh { get; init; }
    public Pipeline Pipeline { get; init; }

    public RenderDataType DataType { get; init; }
    public float3 Color { get; init; }
    public TextureId TextureId { get; init; }
    public RenderFlags Flags { get; init; }
    
    public UInt32 ItemId { get; init; }
}