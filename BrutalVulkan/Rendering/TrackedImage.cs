using Armature.Vulkan;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering;

public sealed class TrackedImage : IDisposable
{
    public ImageEx Image { get; }
    public VkImageLayout Layout { get; private set;  }
    private VkPipelineStageFlags _stage;
    private VkAccessFlags _access;

    public TrackedImage(ImageEx image)
    {
        Image = image;
        Layout = VkImageLayout.Undefined;
        _stage = VkPipelineStageFlags.TopOfPipeBit;
        _access = VkAccessFlags.None;
    }

    public void TransitionTo(CommandBuffer cmd,
        VkImageLayout newLayout,
        VkPipelineStageFlags dstStage,
        VkAccessFlags dstAccess,
        VkImageAspectFlags aspect = VkImageAspectFlags.ColorBit)
    {
        var wasPrevWrite = (_access & (VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.ShaderWriteBit)) != 0;
        if (Layout == newLayout && _stage == dstStage && _access == dstAccess && !wasPrevWrite) return;
        
        ImageUtility.TransitionImage(Image.VkImage, Layout, newLayout, _stage, dstStage, _access, dstAccess, aspect, cmd);

        Layout = newLayout;
        _stage = dstStage;
        _access = dstAccess;
    }

    public void Dispose()
    {
        Image.Dispose();
    }
}