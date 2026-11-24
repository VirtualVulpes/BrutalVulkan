using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan.Rendering;

public sealed class RenderTargets : IDisposable
{
    public VkExtent2D Extent { get; }
    
    public RenderTarget Color;
    public RenderTarget ResolvedColor;
    public RenderTarget Depth;
    public RenderTarget ResolvedDepth;
    public RenderTarget PostProcess;
    
    public RenderTargets(VmaAllocator allocator, VkFormat swapchainFormat, VkExtent2D extent, VkSampleCountFlags msaa)
    {
        Extent = extent;
        
        Color = new RenderTarget(allocator, "Color Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit, samples: msaa);
        
        Depth = new RenderTarget(allocator, "Depth Render Target", VkFormat.D32SFloat, extent,
            VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.TransientAttachmentBit, aspect: VkImageAspectFlags.DepthBit, samples: msaa);
        
        ResolvedColor = new RenderTarget(allocator, "Resolved Color Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);

        ResolvedDepth = new RenderTarget(allocator, "Resolved Depth Render Target", VkFormat.D32SFloat, extent,
            VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.SampledBit, aspect: VkImageAspectFlags.DepthBit);
        
        PostProcess = new RenderTarget(allocator, "Post Process Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);
    }
    
    public void Dispose()
    {
        Color.Dispose();
        ResolvedColor.Dispose();
        Depth.Dispose();
        ResolvedDepth.Dispose();
        PostProcess.Dispose();
    }
}