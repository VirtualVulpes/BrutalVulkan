using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan;

public sealed class Pipeline : IDisposable
{
    private readonly DeviceEx _device;
    
    public VkPipeline Handle { get; }
    public VkPipelineLayout Layout { get; }

    public Pipeline(DeviceEx device, VkPipeline handle, VkPipelineLayout layout)
    {
        _device = device;

        Handle = handle;
        Layout = layout;
    }
    
    public void Dispose()
    {
        _device.DestroyPipeline(Handle, null);
        _device.DestroyPipelineLayout(Layout, null);
    }
}