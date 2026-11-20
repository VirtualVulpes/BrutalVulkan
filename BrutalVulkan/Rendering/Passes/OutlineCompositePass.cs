using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Passes;

public class OutlineCompositePass : IRenderPass
{
    private Pipeline _pipeline;

    public OutlineCompositePass(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.FrameResources.PostProcessImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);

        context.FrameResources.SeedAImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.General, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.SwapchainImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Load,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
        };

        context.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(context.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment
            }
        );

        context.CommandBuffer.SetViewport(0, [
            new VkViewport
            {
                X = 0.0f,
                Y = context.Extent.Height,
                Width = context.Extent.Width,
                Height = -context.Extent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            }
        ]);
        context.CommandBuffer.SetScissor(0, [new VkRect2D(context.Extent)]);

        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);
        context.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0,
                [context.FrameResources.OutlineCompositeDescriptorSet],
                null);

        context.CommandBuffer.Draw(3, 1, 0, 0);

        context.CommandBuffer.EndRendering();
    }
}