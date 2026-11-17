using Brutal.VulkanApi;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes.Compute;

public class TestComputePass : IRenderPass
{
    private readonly Pipeline _pipeline;

    public TestComputePass(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Compute, _pipeline.Handle);
        context.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Compute, _pipeline.Layout, 0, [context.FrameResources.ComputeDescriptorSet],
                null);

        const Int32 localSizeX = 16;
        const Int32 localSizeY = 16;
        
        context.CommandBuffer.Dispatch((context.Extent.Width + localSizeX - 1) / localSizeY, (context.Extent.Height + localSizeX - 1) / localSizeY, 1);
    }
}