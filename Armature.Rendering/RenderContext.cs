using Brutal.Numerics;
using Brutal.VulkanApi;

namespace Armature.Rendering;

public sealed class RenderContext
{
    public FrameResources FrameResources { get; init; }

    public RenderTargets Targets { get; init; }

    public VkImageView SwapchainImageView { get; init; }
    
    public VkDescriptorSet GlobalDescriptorSet { get; init; }
    public VkDescriptorSet PerFrameDescriptorSet { get; set; }

    public Int32 FrameIndex { get; init; }

    public CommandBuffer CommandBuffer => FrameResources.CommandBuffer;
    
    public float4x4 ViewProj { get; init; }
}