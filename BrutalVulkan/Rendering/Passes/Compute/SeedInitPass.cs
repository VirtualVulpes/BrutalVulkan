using Brutal.VulkanApi;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Passes.Compute;

public class SeedInitPass : IRenderPass, ITargetConsumer
{
    private readonly Pipeline _pipeline;
    private readonly DescriptorManager _descriptors;
    private readonly VkDescriptorSet _descriptorSet;

    public SeedInitPass(Pipeline pipeline, DescriptorManager descriptors)
    {
        _pipeline = pipeline;
        _descriptors = descriptors;
        _descriptorSet = descriptors.AllocateSeedInitDescriptorSet();
    }

    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> _)
    {
        context.Targets.SelectionMask.Image.TransitionTo(context.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderReadBit);

        context.Targets.SeedA.Image.TransitionTo(context.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderWriteBit);

        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Compute, _pipeline.Handle);
        context.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Compute, _pipeline.Layout, 0,
                [_descriptorSet],
                null);

        const Int32 localSizeX = 16;
        const Int32 localSizeY = 16;

        context.CommandBuffer.Dispatch((context.Targets.Extent.Width + localSizeX - 1) / localSizeX,
            (context.Targets.Extent.Height + localSizeY - 1) / localSizeY, 1);
    }

    public void OnTargetsRecreated(RenderTargets targets)
    {
        _descriptors.UpdateSeedDescriptor(_descriptorSet, targets.SelectionMask.View.VkImageView,
            targets.SeedA.View.VkImageView);
    }
}