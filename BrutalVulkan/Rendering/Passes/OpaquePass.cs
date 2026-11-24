using Armature.Core;
using Armature.Vulkan;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public sealed class OpaquePass : IRenderPass
{
    public unsafe void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects)
    {
        ctx.Targets.Color.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        ctx.Targets.ResolvedColor.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        ctx.Targets.Depth.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit, VkAccessFlags.DepthStencilAttachmentWriteBit, VkImageAspectFlags.DepthBit);
        
        ctx.Targets.ResolvedDepth.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.EarlyFragmentTestsBit, VkAccessFlags.DepthStencilAttachmentWriteBit, VkImageAspectFlags.DepthBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = ctx.Targets.Color.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) },
            
            ResolveMode = VkResolveModeFlags.AverageBit,
            ResolveImageView = ctx.Targets.ResolvedColor.View.VkImageView,
            ResolveImageLayout = VkImageLayout.ColorAttachmentOptimal
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = ctx.Targets.Depth.View.VkImageView,
            ImageLayout = VkImageLayout.DepthAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.DontCare,
            ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 },
            
            ResolveMode = VkResolveModeFlags.SampleZeroBit,
            ResolveImageView = ctx.Targets.ResolvedDepth.View.VkImageView,
            ResolveImageLayout = VkImageLayout.DepthAttachmentOptimal
        };

        ctx.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(ctx.Targets.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthAttachment = &depthAttachment
            }
        );

        ctx.CommandBuffer.SetViewport(0, [
            new VkViewport
            {
                X = 0.0f,
                Y = ctx.Targets.Extent.Height,
                Width = ctx.Targets.Extent.Width,
                Height = -ctx.Targets.Extent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            }
        ]);
        ctx.CommandBuffer.SetScissor(0, [new VkRect2D(ctx.Targets.Extent)]);

        var cachedPipelineId = PipelineId.Invalid;
        Pipeline? pipeline = null;

        foreach (var renderObject in renderObjects)
        {
            if (cachedPipelineId != renderObject.Pipeline.Id)
            {
                cachedPipelineId = renderObject.Pipeline.Id;
                pipeline = renderObject.Pipeline;

                ctx.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline.Handle);
                ctx.CommandBuffer
                    .BindDescriptorSets(VkPipelineBindPoint.Graphics, pipeline.Layout, 0, [ctx.GlobalDescriptorSet, ctx.FrameResources.PerFrameDescriptorSet],
                        null);
            }

            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * ctx.Scene.Camera.ViewProj,
                Model = renderObject.ModelMatrix
            };

            ctx.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                pushConsts);

            switch (renderObject.DataType)
            {
                case RenderDataType.Color:
                    var colorFrag = new PushConstantBlocks.ColorFragmentBlock
                    {
                        CameraPosition = ctx.Scene.Camera.Position,
                        Color = renderObject.Color
                    };
                    ctx.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), colorFrag);
                    break;
                case RenderDataType.Texture:
                    var textureFrag = new PushConstantBlocks.TextureFragmentBlock
                    {
                        TextureId = renderObject.TextureId.Value,
                        CameraPosition = ctx.Scene.Camera.Position
                    };

                    ctx.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), textureFrag);
                    break;
            }

            var gpuMesh = renderObject.GpuMesh;

            ctx.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            ctx.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            ctx.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        ctx.CommandBuffer.EndRendering();
    }
}