using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.Editor;

public sealed class EditorTargets : IDisposable
{
    private readonly VmaAllocator _allocator;
    
    public VkExtent2D Extent { get; private set; }
    
    public RenderTarget EntityId;
    public RenderTarget EntityIdDepth;
    public RenderTarget SelectionMask;
    public RenderTarget SeedA;
    public RenderTarget SeedB;
    
    public EditorTargets(VmaAllocator allocator, VkExtent2D extent)
    {
        _allocator = allocator;
        Create(extent);
    }

    public void Recreate(VkExtent2D extent)
    {
        DisposeTargets();
        Create(extent);
    }

    private void Create(VkExtent2D extent)
    {
        Extent = extent;
        
        EntityId = new RenderTarget(_allocator, "Entity Id Render Target", VkFormat.R32UInt, extent,
            VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit);

        EntityIdDepth = new RenderTarget(_allocator, "Entity Id Render Target", VkFormat.D32SFloat, extent,
            VkImageUsageFlags.DepthStencilAttachmentBit, aspect: VkImageAspectFlags.DepthBit);
        
        SelectionMask = new RenderTarget(_allocator, "Selection Mask Render Target", VkFormat.R8UNorm, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit);

        SeedA = new RenderTarget(_allocator, "Seed A Render Target", VkFormat.R32G32SInt, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);
        
        SeedB = new RenderTarget(_allocator, "Seed B Render Target", VkFormat.R32G32SInt, extent,
            VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);
    }

    private void DisposeTargets()
    {
        EntityId.Dispose();
        EntityIdDepth.Dispose();
        SelectionMask.Dispose();
        SeedA.Dispose();
        SeedB.Dispose();
    }

    public void Dispose() => DisposeTargets();
}