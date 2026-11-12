using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan;

public sealed class OpaquePass : IRenderPass
{
    private readonly PipelineManager _pipelineManager;

    public OpaquePass(PipelineManager pipelineManager)
    {
        _pipelineManager = pipelineManager;
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderObject> renderObjects)
    {
        App.TransitionImage(
            context.ColorImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            context.CommandBuffer
        );

        App.TransitionImage(
            context.DepthImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
            VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
            VkAccessFlags.DepthStencilAttachmentWriteBit,
            VkAccessFlags.DepthStencilAttachmentWriteBit,
            VkImageAspectFlags.DepthBit,
            context.CommandBuffer
        );

        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.ColorImageView.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            ResolveMode = VkResolveModeFlags.AverageBit,
            ResolveImageView = context.ResolveImageView,
            ResolveImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.DontCare,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
        };

        var depthAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.DepthImageView.VkImageView,
            ImageLayout = VkImageLayout.DepthAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.DontCare,
            ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
        };

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

        var cachedPipelineId = PipelineId.Invalid;
        Pipeline? pipeline = null;

        foreach (var renderObject in renderObjects)
        {
            if (cachedPipelineId != renderObject.Material.PipelineId)
            {
                cachedPipelineId = renderObject.Material.PipelineId;
                pipeline = _pipelineManager.GetPipeline(cachedPipelineId);

                context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline.Handle);
                context.CommandBuffer
                    .BindDescriptorSets(VkPipelineBindPoint.Graphics, pipeline.Layout, 0, context.GlobalDescriptorSets,
                        null);
            }

            context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                renderObject.ModelMatrix * context.ViewProj);
            context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                ByteSize.Of<float4x4>(),
                renderObject.Material.TextureId);

            var gpuMesh = renderObject.Mesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }

        context.CommandBuffer.EndRendering();
    }
}