using Armature.Vulkan;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Editor;

namespace BrutalVulkan.Rendering.Passes;

public class EntityIdPass : IRenderPass
{
    private readonly Pipeline _pipeline;
    private readonly EditorTargets _editor;

    public EntityIdPass(Pipeline pipeline, EditorTargets editor)
    {
        _pipeline = pipeline;
        _editor = editor;
    }

    public unsafe void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects)
    {
        _editor.EntityId.Image.TransitionTo(ctx.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.ColorAttachmentWriteBit);

        _editor.EntityIdDepth.Image.TransitionTo(ctx.CommandBuffer,
            VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags.EarlyFragmentTestsBit,
            VkAccessFlags.DepthStencilAttachmentReadBit, VkImageAspectFlags.DepthBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = _editor.EntityId.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Uint32 = new uint4(UInt32.MaxValue, 0, 0, 0) }
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = _editor.EntityIdDepth.View.VkImageView,
            ImageLayout = VkImageLayout.DepthAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.DontCare,
            ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
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

        ctx.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);

        foreach (var renderObject in renderObjects)
        {
            var vertPushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * ctx.Scene.Camera.ViewProj,
                Model = renderObject.ModelMatrix
            };

            var fragPushConsts = new PushConstantBlocks.EntityIdBlock
            {
                Id = renderObject.ItemId
            };

            ctx.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                vertPushConsts);

            ctx.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.FragmentBit, ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                fragPushConsts);

            var gpuMesh = renderObject.GpuMesh;

            ctx.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            ctx.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            ctx.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        ctx.CommandBuffer.EndRendering();
    }
}