using Armature.Core;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Vulkan;

public sealed class Pipeline : IDisposable
{
    private readonly DeviceEx _device;
    
    public VkPipeline Handle { get; }
    public VkPipelineLayout Layout { get; }
    
    public PipelineId Id { get; }

    public Pipeline(DeviceEx device, VkPipeline handle, VkPipelineLayout layout, PipelineId id)
    {
        _device = device;

        Handle = handle;
        Layout = layout;
        Id = id;
    }
    
    public void Dispose()
    {
        _device.DestroyPipeline(Handle, null);
        _device.DestroyPipelineLayout(Layout, null);
    }
}