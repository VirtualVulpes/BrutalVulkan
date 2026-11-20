using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public class AtmospherePass : IRenderPass
{
    private Pipeline _pipeline;

    public AtmospherePass(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.FrameResources.ResolveColorImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);
        
        context.FrameResources.PostProcessImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.FrameResources.PostProcessImageView.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
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
                [context.GlobalDescriptorSet, context.FrameResources.AtmosphereDescriptorSet],
                null);

        var pushConsts = new PushConstantBlocks.AtmosphereBlock
        {
            Center = new float3(0.0f, 0.0f, 0.0f),
            Radius = 1.05f,
            LightPosition = new float3(-10.0f, 10.0f, 0.0f),
        };
        
        context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.FragmentBit, ByteSize.Zero, pushConsts);

        context.CommandBuffer.Draw(3, 1, 0, 0);

        context.CommandBuffer.EndRendering();
    }
}