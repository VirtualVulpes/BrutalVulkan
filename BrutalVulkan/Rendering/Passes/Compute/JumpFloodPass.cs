using System.Diagnostics;
using Brutal;
using Brutal.VulkanApi;
using BrutalVulkan.Rendering.Pipelines;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering.Passes.Compute;

public class JumpFloodPass : IRenderPass, ITargetConsumer
{
    private readonly Pipeline _pipeline;
    private readonly DescriptorManager _descriptors;
    private readonly VkDescriptorSet _bToASet;
    private readonly VkDescriptorSet _aToBSet;

    private const UInt32 StartJump = 2;

    public JumpFloodPass(Pipeline pipeline, DescriptorManager descriptors)
    {
        Debug.Assert((UInt32.Log2(StartJump) + 1) % 2 == 0,
            "Jump flood needs an even pass count so the result ends in SeedA");
        
        _pipeline = pipeline;
        _descriptors = descriptors;
        _aToBSet = descriptors.AllocateSeedAToBDescriptorSet();
        _bToASet = descriptors.AllocateSeedBToADescriptorSet();
    }

    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> _)
    {
        context.Targets.SeedA.Image.TransitionTo(context.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderReadBit | VkAccessFlags.ShaderWriteBit);
        context.Targets.SeedB.Image.TransitionTo(context.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderReadBit | VkAccessFlags.ShaderWriteBit);

        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Compute, _pipeline.Handle);

        var isPassA = true;

        for (var i = StartJump; i >= 1; i >>= 1)
        {
            context.CommandBuffer.PipelineBarrier(
                VkPipelineStageFlags.ComputeShaderBit,
                VkPipelineStageFlags.ComputeShaderBit,
                VkDependencyFlags.None,
                [
                    new VkMemoryBarrier
                    {
                        SrcAccessMask = VkAccessFlags.ShaderWriteBit,
                        DstAccessMask = VkAccessFlags.ShaderReadBit,
                    }
                ],
                [],
                []
            );

            context.CommandBuffer
                .BindDescriptorSets(VkPipelineBindPoint.Compute, _pipeline.Layout, 0,
                    [
                        isPassA
                            ? _aToBSet
                            : _bToASet
                    ],
                    null);

            var pushConsts = new PushConstantBlocks.JumpPassBlock
            {
                JumpDistance = i
            };

            context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.ComputeBit, ByteSize.Zero,
                pushConsts);

            const Int32 localSizeX = 16;
            const Int32 localSizeY = 16;

            context.CommandBuffer.Dispatch((context.Targets.Extent.Width + localSizeX - 1) / localSizeX,
                (context.Targets.Extent.Height + localSizeY - 1) / localSizeY, 1);

            isPassA = !isPassA;
        }
    }


    public void OnTargetsRecreated(RenderTargets targets)
    {
        _descriptors.UpdateSeedDescriptor(_aToBSet,
            targets.SeedA.View.VkImageView, targets.SeedB.View.VkImageView);
        _descriptors.UpdateSeedDescriptor(_bToASet,
            targets.SeedB.View.VkImageView, targets.SeedA.View.VkImageView);
    }
}