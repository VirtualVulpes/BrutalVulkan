using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class FrameResources
{
    public CommandBuffer CommandBuffer;
    public VkFence Fence;
    public VkSemaphore ImageAvailable;
    
    public BufferEx LightsBuffer;
    public BufferEx CameraBuffer;
    public MappedMemory LightsMemory;
    public MappedMemory CameraMemory;

    public VkDescriptorSet PerFrameDescriptorSet;

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