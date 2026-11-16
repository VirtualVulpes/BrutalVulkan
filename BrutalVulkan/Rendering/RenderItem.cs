using Brutal.Numerics;
using BrutalVulkan.Rendering.Meshes;

namespace BrutalVulkan.Rendering;

public readonly struct RenderItem
{
    public float4x4 ModelMatrix { get; init; }

    public GpuMesh GpuMesh { get; init; }
    public Pipeline Pipeline { get; init; }

    public RenderDataType DataType { get; init; }
    public float3 Color { get; init; }
    public TextureId TextureId { get; init; }
}