using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.Materials;

public class ColorMaterial : Material
{
    public float3 Color { get; init; }
    
    public override void Bind(RenderContext context, Pipeline pipeline)
    {
        context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
            ByteSize.Of<float4x4>(), Color);    
    }
}