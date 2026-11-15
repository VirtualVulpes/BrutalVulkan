using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.Materials;

public sealed class TextureMaterial : Material
{
    public TextureId TextureId { get; init; }
    
    public override void Bind(RenderContext context, Pipeline pipeline)
    {
        context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
            ByteSize.Of<float4x4>(), TextureId);
    }
}