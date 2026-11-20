using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public struct FrameResources
{
    public CommandBuffer CommandBuffer;
    public VkFence Fence;
    public VkSemaphore ImageAvailable;

    public TrackedImage ColorImage;
    public ImageViewEx ColorImageView;

    public TrackedImage DepthImage;
    public ImageViewEx DepthImageView;
    
    public TrackedImage ResolveColorImage;
    public ImageViewEx ResolveColorImageView;
    
    public TrackedImage PostProcessImage;
    public ImageViewEx PostProcessImageView;
    
    public TrackedImage SelectionMaskImage;
    public ImageViewEx SelectionMaskImageView;
    
    public TrackedImage SeedAImage;
    public ImageViewEx SeedAImageView;

    public TrackedImage SeedBImage;
    public ImageViewEx SeedBImageView;
    
    public TrackedImage EntityIdImage;
    public ImageViewEx EntityIdImageView;
    
    public TrackedImage EntityIdDepthImage;
    public ImageViewEx EntityIdDepthImageView;
    
    public VkDescriptorSet OutlineCompositeDescriptorSet;
    public VkDescriptorSet SeedDescriptorSet;
    public VkDescriptorSet SeedAToBDescriptorSet;
    public VkDescriptorSet SeedBToADescriptorSet;
    public VkDescriptorSet AtmosphereDescriptorSet;
}