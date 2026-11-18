using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public class EntityIdPass
{
    private Pipeline _pipeline;

    public EntityIdPass(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.FrameResources.EntityIdImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.ColorAttachmentWriteBit);

        context.FrameResources.EntityIdDepthImage.TransitionTo(context.CommandBuffer,
            VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags.EarlyFragmentTestsBit,
            VkAccessFlags.DepthStencilAttachmentWriteBit, VkImageAspectFlags.DepthBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.FrameResources.EntityIdImageView.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Uint32 = new uint4(UInt32.MaxValue, 0, 0, 0) }
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.FrameResources.EntityIdDepthImageView.VkImageView,
            ImageLayout = VkImageLayout.DepthAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
        };
        
        context.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(context.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthAttachment = &depthAttachment
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
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0, [context.GlobalDescriptorSet],
                null);

        foreach (var renderObject in renderObjects)
        {
            var vertPushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * context.ViewProj,
                Model = renderObject.ModelMatrix
            };

            var fragPushConsts = new PushConstantBlocks.EntityIdBlock
            {
                Id = renderObject.Entity.Id.Value + (renderObject.Entity.Generation << 16)
            };

            context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                vertPushConsts);

            context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.FragmentBit, ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                fragPushConsts);

            var gpuMesh = renderObject.GpuMesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        context.CommandBuffer.EndRendering();
    }
}