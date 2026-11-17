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
        ImageUtility.TransitionImage(
            context.FrameResources.SelectionMaskImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            context.CommandBuffer
        );
        
        ImageUtility.TransitionImage(
            context.FrameResources.SelectionMaskDepthImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
            VkAccessFlags.None,
            VkAccessFlags.DepthStencilAttachmentWriteBit,
            VkImageAspectFlags.DepthBit,
            context.CommandBuffer
        );
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.FrameResources.SelectionMaskImageView.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = float4.Zero }
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.FrameResources.SelectionMaskDepthImageView.VkImageView,
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
            if (!renderObject.IsSelected) continue;
            
            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * context.ViewProj,
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