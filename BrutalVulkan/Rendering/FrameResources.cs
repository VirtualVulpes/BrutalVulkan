using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public struct FrameResources
{
    // Sync / Per-Frame
    
    public CommandBuffer CommandBuffer;
    public VkFence Fence;
    public VkSemaphore ImageAvailable;
    
    public BufferEx LightsBuffer;
    public BufferEx CameraBuffer;
    public MappedMemory LightsMemory;
    public MappedMemory CameraMemory;
    
    // Targets

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
    
    // Descriptor Sets

    public VkDescriptorSet PerFrameDescriptorSet;
    public VkDescriptorSet OutlineCompositeDescriptorSet;
    public VkDescriptorSet SeedDescriptorSet;
    public VkDescriptorSet SeedAToBDescriptorSet;
    public VkDescriptorSet SeedBToADescriptorSet;
    public VkDescriptorSet AtmosphereDescriptorSet;

    public void DisposeTargets()
    {
        ColorImageView.Dispose();
        ColorImage.Dispose();

        DepthImageView.Dispose();
        DepthImage.Dispose();

        ResolveColorImageView.Dispose();
        ResolveColorImage.Dispose();

        PostProcessImageView.Dispose();
        PostProcessImage.Dispose();

        SelectionMaskImageView.Dispose();
        SelectionMaskImage.Dispose();

        SeedAImageView.Dispose();
        SeedAImage.Dispose();

        SeedBImageView.Dispose();
        SeedBImage.Dispose();

        EntityIdImageView.Dispose();
        EntityIdImage.Dispose();

        EntityIdDepthImageView.Dispose();
        EntityIdDepthImage.Dispose();
    }

    public void DestroySync(DeviceEx device)
    {
        device.DestroyFence(Fence, null);
        device.DestroySemaphore(ImageAvailable, null);
        
        LightsMemory.Dispose();
        CameraMemory.Dispose();
        LightsBuffer.Dispose();
        CameraBuffer.Dispose();
    }
}