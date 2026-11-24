using Armature.Rendering;
using Armature.Rendering.Passes;
using Armature.Vulkan;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Editor.Passes;

public class SelectionMaskPass : IRenderPass
{
    private Pipeline _pipeline;
    private EditorTargets _editor;
    
    public SelectionMaskPass(Pipeline pipeline, EditorTargets editor)
    {
        _pipeline = pipeline;
        _editor = editor;
    }

    public unsafe void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects)
    {
        _editor.SelectionMask.Image.TransitionTo(ctx.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.ColorAttachmentWriteBit);

        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = _editor.SelectionMask.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = float4.Zero }
        };

        ctx.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(ctx.Targets.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment
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
        ctx.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0, [ctx.GlobalDescriptorSet],
                null);

        foreach (var renderObject in renderObjects)
        {
            if ((renderObject.Flags & RenderFlags.Selected) == 0) continue;

            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * ctx.ViewProj,
                Model = renderObject.ModelMatrix
            };

            ctx.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                pushConsts);

            var gpuMesh = renderObject.GpuMesh;

            ctx.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            ctx.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            ctx.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        ctx.CommandBuffer.EndRendering();
    }
}