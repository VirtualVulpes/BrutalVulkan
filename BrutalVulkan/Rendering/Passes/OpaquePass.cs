using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public sealed class OpaquePass : IRenderPass
{
    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.Targets.Color.Image.TransitionTo(context.CommandBuffer, VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        context.Targets.Resolve.Image.TransitionTo(context.CommandBuffer, VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        context.Targets.Depth.Image.TransitionTo(context.CommandBuffer, VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.EarlyFragmentTestsBit, VkAccessFlags.DepthStencilAttachmentWriteBit, VkImageAspectFlags.DepthBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.Targets.Color.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            ResolveMode = VkResolveModeFlags.AverageBit,
            ResolveImageView = context.Targets.Resolve.View.VkImageView,
            ResolveImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.Targets.Depth.View.VkImageView,
            ImageLayout = VkImageLayout.DepthAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
        };

        context.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(context.Targets.Extent),
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
                Y = context.Targets.Extent.Height,
                Width = context.Targets.Extent.Width,
                Height = -context.Targets.Extent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            }
        ]);
        context.CommandBuffer.SetScissor(0, [new VkRect2D(context.Targets.Extent)]);

        var cachedPipelineId = PipelineId.Invalid;
        Pipeline? pipeline = null;

        foreach (var renderObject in renderObjects)
        {
            if (cachedPipelineId != renderObject.Pipeline.Id)
            {
                cachedPipelineId = renderObject.Pipeline.Id;
                pipeline = renderObject.Pipeline;

                context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline.Handle);
                context.CommandBuffer
                    .BindDescriptorSets(VkPipelineBindPoint.Graphics, pipeline.Layout, 0, [context.GlobalDescriptorSet, context.FrameResources.PerFrameDescriptorSet],
                        null);
            }

            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * context.Camera.View * context.Camera.Proj,
                Model = renderObject.ModelMatrix
            };

            context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                pushConsts);

            switch (renderObject.DataType)
            {
                case RenderDataType.Color:
                    var colorFrag = new PushConstantBlocks.ColorFragmentBlock
                    {
                        CameraPosition = context.Camera.Position,
                        Color = renderObject.Color
                    };
                    context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), colorFrag);
                    break;
                case RenderDataType.Texture:
                    var textureFrag = new PushConstantBlocks.TextureFragmentBlock
                    {
                        TextureId = renderObject.TextureId.Value,
                        CameraPosition = context.Camera.Position
                    };

                    context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), textureFrag);
                    break;
            }

            var gpuMesh = renderObject.GpuMesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        context.CommandBuffer.EndRendering();
    }
}