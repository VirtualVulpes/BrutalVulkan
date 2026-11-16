using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Utility;

public static class ImageUtility
{
    public static void TransitionImage(
        VkImage image,
        VkImageLayout from,
        VkImageLayout to,
        VkPipelineStageFlags srcStage,
        VkPipelineStageFlags dstStage,
        VkAccessFlags srcAccess,
        VkAccessFlags dstAccess,
        VkImageAspectFlags imageAspectFlags,
        CommandBuffer commandBuffer,
        Int32 mipLevels = 1,
        Int32 baseMipLevel = 0
    )
    {
        commandBuffer.PipelineBarrier(
            srcStage,
            dstStage,
            VkDependencyFlags.None,
            [],
            [],
            [
                new VkImageMemoryBarrier
                {
                    SrcAccessMask = srcAccess,
                    DstAccessMask = dstAccess,
                    OldLayout = from,
                    NewLayout = to,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = image,
                    SubresourceRange = new VkImageSubresourceRange
                    {
                        AspectMask = imageAspectFlags,
                        LayerCount = 1,
                        LevelCount = mipLevels,
                        BaseArrayLayer = 0,
                        BaseMipLevel = baseMipLevel
                    }
                }
            ]
        );
    }

    public static void CopyBufferToImage(CommandBuffer commandBuffer, VkBuffer buffer, ImageEx image, Int32 width,
        Int32 height)
    {
        var region = new VkBufferImageCopy
        {
            BufferOffset = new ByteSize64(0),
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
                { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1 },
            ImageOffset = new VkOffset3D(0, 0, 0),
            ImageExtent = { Width = width, Height = height, Depth = 1 }
        };
        commandBuffer.CopyBufferToImage(buffer, image.VkImage, VkImageLayout.TransferDstOptimal, [region]);
    }
}