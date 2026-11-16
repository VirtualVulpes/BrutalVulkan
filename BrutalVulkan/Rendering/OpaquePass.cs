using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering;

public sealed class OpaquePass : IRenderPass
{
    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
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
            if (cachedPipelineId != renderObject.Pipeline.Id)
            {
                cachedPipelineId = renderObject.Pipeline.Id;
                pipeline = renderObject.Pipeline;

                context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline.Handle);
                context.CommandBuffer
                    .BindDescriptorSets(VkPipelineBindPoint.Graphics, pipeline.Layout, 0, context.GlobalDescriptorSets,
                        null);
            }

            var pushConsts = new PushConstantBlocks.VertexBlock
            {
                Mvp = renderObject.ModelMatrix * context.ViewProj,
                Model = renderObject.ModelMatrix
            };

            context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                pushConsts);

            switch (renderObject.DataType)
            {
                case RenderDataType.Color:
                    context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), renderObject.Color);
                    break;
                case RenderDataType.Texture:
                    var fragment = new PushConstantBlocks.FragmentBlock
                    {
                        TextureId = renderObject.TextureId.Value,
                        CameraPosition = context.CameraPosition
                    };

                    context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.FragmentBit,
                        ByteSize.Of<PushConstantBlocks.VertexBlock>(), fragment);
                    break;
            }

            var gpuMesh = renderObject.GpuMesh;

            context.CommandBuffer.BindVertexBuffers(0, [gpuMesh.VertexBuffer], [new ByteSize64(0)]);
            context.CommandBuffer.BindIndexBuffer(gpuMesh.IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
            context.CommandBuffer.DrawIndexed(gpuMesh.IndexCount, 1, 0, 0, 0);
        }
    }
}