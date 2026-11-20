using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.Rendering.GpuTypes;

namespace BrutalVulkan.Rendering;

public readonly struct RenderContext
{
    public FrameResources FrameResources { get; init; }
    
    public VkImageView SwapchainImageView { get; init; }

    public VkExtent2D Extent { get; init; }

    public GpuCamera Camera { get; init; }
    
    public VkDescriptorSet GlobalDescriptorSet { get; init; }
    
    public Int32 FrameIndex { get; init; }

    public CommandBuffer CommandBuffer => FrameResources.CommandBuffer;
}