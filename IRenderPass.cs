using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan;

public readonly struct RenderContext
{
    public CommandBuffer CommandBuffer { get; init; }

    public ImageEx ColorImage { get; init; }
    public ImageViewEx ColorImageView { get; init; }

    public ImageEx DepthImage { get; init; }
    public ImageViewEx DepthImageView { get; init; }

    public VkImageView ResolveImageView { get; init; }

    public VkExtent2D Extent { get; init; }
    public float4x4 ViewProj { get; init; }

    public VkDescriptorSet[] GlobalDescriptorSets { get; init; }
}

public interface IRenderPass
{
    public void Record(in RenderContext context, ReadOnlySpan<RenderObject> renderObjects);
}