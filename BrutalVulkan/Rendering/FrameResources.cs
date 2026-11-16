using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public struct FrameResources
{
    public CommandBuffer CommandBuffer;
    public VkFence Fence;
    public VkSemaphore ImageAvailable;

    public ImageEx ColorImage;
    public ImageViewEx ColorImageView;

    public ImageEx DepthImage;
    public ImageViewEx DepthImageView;
}