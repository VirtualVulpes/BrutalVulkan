using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan.Rendering;

public sealed class RenderTargets : IDisposable
{
    public VkExtent2D Extent { get; }
    
    public RenderTarget Color;
    public RenderTarget Depth;
    public RenderTarget Resolve;
    public RenderTarget PostProcess;
    public RenderTarget SelectionMask;
    public RenderTarget SeedA;
    public RenderTarget SeedB;
    public RenderTarget EntityId;
    public RenderTarget EntityIdDepth;
    
    public RenderTargets(VmaAllocator allocator, VkFormat swapchainFormat, VkExtent2D extent, VkSampleCountFlags msaa)
    {
        Extent = extent;
        
        Color = new RenderTarget(allocator, "Color Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit, samples: msaa);
        
        Resolve = new RenderTarget(allocator, "Resolve Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);

        Depth = new RenderTarget(allocator, "Depth Render Target", VkFormat.D32SFloat, extent,
            VkImageUsageFlags.DepthStencilAttachmentBit, aspect: VkImageAspectFlags.DepthBit, samples: msaa);
        
        PostProcess = new RenderTarget(allocator, "Post Process Render Target", swapchainFormat, extent,
            VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);
        
        EntityId = new RenderTarget(allocator, "Entity Id Render Target", VkFormat.R32UInt, extent,
            VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit);
        
        EntityIdDepth = new RenderTarget(allocator, "Entity Id Depth Render Target", VkFormat.D32SFloat, extent,
            VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.SampledBit, aspect: VkImageAspectFlags.DepthBit);

        SelectionMask = new RenderTarget(allocator, "Selection Mask Render Target", VkFormat.R8UNorm, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit);

        SeedA = new RenderTarget(allocator, "Seed A Render Target", VkFormat.R32G32SInt, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);
        
        SeedB = new RenderTarget(allocator, "Seed B Render Target", VkFormat.R32G32SInt, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);
    }
    
    public void Dispose()
    {
        Color.Dispose();
        Depth.Dispose();
        Resolve.Dispose();
        PostProcess.Dispose();
        SelectionMask.Dispose();
        SeedA.Dispose();
        SeedB.Dispose();
        EntityId.Dispose();
        EntityIdDepth.Dispose();    
    }
}