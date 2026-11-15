using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class OpaquePass : IRenderPass
{
    private readonly PipelineManager _pipelineManager;

    public OpaquePass(PipelineManager pipelineManager)
    {
        _pipelineManager = pipelineManager;
    }

    public void Record(in RenderContext context, ReadOnlySpan<RenderObject> renderObjects)
    {
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
            renderObject.Material.Bind(context, pipeline);

            var gpuMesh = renderObject.Mesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }
    }
}