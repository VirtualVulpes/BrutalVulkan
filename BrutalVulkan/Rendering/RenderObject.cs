using Brutal.Numerics;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering;

public readonly struct RenderObject
{
    public float4x4 ModelMatrix { get; init; }

    public MeshId MeshId { get; init; }
    public PipelineId PipelineId { get; init; }

    public RenderDataType DataType { get; init; }
    public float3 Color { get; init; }
    public TextureId TextureId { get; init; }
    public Boolean IsSelected { get; init; }
}