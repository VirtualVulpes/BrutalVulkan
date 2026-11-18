using Brutal.Numerics;
using Brutal.VulkanApi;

namespace BrutalVulkan.Rendering;

public readonly struct RenderContext
{
    public FrameResources FrameResources { get; init; }
    
    public VkImageView SwapchainImageView { get; init; }

    public VkExtent2D Extent { get; init; }
    public float4x4 ViewProj { get; init; }
    public float3 CameraPosition { get; init; }

    public VkDescriptorSet GlobalDescriptorSet { get; init; }
    
    public Int32 FrameIndex { get; init; }

    public CommandBuffer CommandBuffer => FrameResources.CommandBuffer;
}