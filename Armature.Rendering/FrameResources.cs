using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Rendering;

public sealed class FrameResources
{
    public CommandBuffer CommandBuffer;
    public VkFence Fence;
    public VkSemaphore ImageAvailable;
    
    public void DestroySync(DeviceEx device)
    {
        device.DestroyFence(Fence, null);
        device.DestroySemaphore(ImageAvailable, null);
    }
}