using Brutal.VulkanApi;

namespace BrutalVulkan.Rendering;

public readonly struct RenderContext
{
    public FrameResources FrameResources { get; init; }

    public RenderTargets Targets { get; init; }

    public VkImageView SwapchainImageView { get; init; }
    
    public VkDescriptorSet GlobalDescriptorSet { get; init; }

    public Int32 FrameIndex { get; init; }

    public CommandBuffer CommandBuffer => FrameResources.CommandBuffer;

    public SceneData Scene { get; init; }
}