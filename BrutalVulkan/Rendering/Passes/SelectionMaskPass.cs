using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Passes;

public class SelectionMaskPass : IRenderPass
{
    private Pipeline _pipeline;

    public SelectionMaskPass(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.Targets.SelectionMask.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.ColorAttachmentWriteBit);

        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.Targets.SelectionMask.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = float4.Zero }
        };

        context.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(context.Targets.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment
            }
        );

        context.CommandBuffer.SetViewport(0, [
            new VkViewport
            {
                X = 0.0f,
                Y = context.Targets.Extent.Height,
                Width = context.Targets.Extent.Width,
                Height = -context.Targets.Extent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            }
        ]);
        context.CommandBuffer.SetScissor(0, [new VkRect2D(context.Targets.Extent)]);

        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);
        context.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0, [context.GlobalDescriptorSet],
                null);

        foreach (var renderObject in renderObjects)
        {
            if (!renderObject.IsSelected) continue;

            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * context.Camera.View * context.Camera.Proj,
                Model = renderObject.ModelMatrix
            };

            context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                pushConsts);

            var gpuMesh = renderObject.GpuMesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        context.CommandBuffer.EndRendering();
    }
}