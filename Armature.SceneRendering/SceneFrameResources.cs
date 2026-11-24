using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.SceneRendering;

public sealed class SceneFrameResources : IDisposable
{
    public BufferEx LightsBuffer;
    public BufferEx CameraBuffer;
    public MappedMemory LightsMemory;
    public MappedMemory CameraMemory;
    
    public VkDescriptorSet PerFrameDescriptorSet;

    public void DestroySync()
    {
        LightsMemory.Dispose();
        CameraMemory.Dispose();
        LightsBuffer.Dispose();
        CameraBuffer.Dispose();
    }

    public void Dispose() => DestroySync();
}