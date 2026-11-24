using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace Armature.Rendering;

public sealed class RenderTarget : IDisposable
{
    public TrackedImage Image { get; }
    public ImageViewEx View { get; }

    public RenderTarget(VmaAllocator allocator, String name, VkFormat format, VkExtent2D extent,
        VkImageUsageFlags usage, VkSampleCountFlags samples = VkSampleCountFlags._1Bit,
        VkImageAspectFlags aspect = VkImageAspectFlags.ColorBit)
    {
        Image = new TrackedImage(allocator.CreateImage(new ImageEx.CreateInfo
        {
            Name = name,
            ImageType = VkImageType._2D,
            ImageFormat = format,
            ImageExtent = new VkExtent3D
            {
                Width = extent.Width,
                Height = extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = samples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = usage,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        }));

        View = Image.Image.CreateImageView(VkImageViewType._2D, new VkImageSubresourceRange
        {
            AspectMask = aspect,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        }, null);
    }

    public void Dispose()
    {
        View.Dispose();
        Image.Dispose();
    }
}