using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public class ImGuiPass : IRenderPass
{
    private ImGuiManager _imGuiManager;
    private PipelineManager _pipelineManager;

    public ImGuiPass(ImGuiManager imGuiManager, PipelineManager pipelineManager)
    {
        _imGuiManager = imGuiManager;
        _pipelineManager = pipelineManager;
    }
    
    public void Record(in RenderContext context, ReadOnlySpan<GpuRenderObject> renderObjects)
    {
        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0) return;

        var pipeline = _pipelineManager.GetPipeline("ImGui");
        
        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline.Handle);

        var viewport = new VkViewport
        {
            Width = context.Extent.Width,
            Height = context.Extent.Height,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };
        context.CommandBuffer.SetViewport(0, [viewport]);

        var pushConsts = new PushConstantBlocks.ImGuiBlock
        {
            Scale = new float2(
                2.0f / drawData.DisplaySize.X,
                2.0f / drawData.DisplaySize.Y
            ),
            Translate = new float2(-1.0f, -1.0f)
        };
        context.CommandBuffer.PushConstants(pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero, pushConsts);

        context.CommandBuffer.BindVertexBuffer(0, _imGuiManager.GetVertexBuffer(context.FrameIndex), ByteSize.Zero);
        context.CommandBuffer.BindIndexBuffer(_imGuiManager.GetIndexBuffer(context.FrameIndex), VkIndexType.UInt16);

        var vertexOffset = 0;
        var indexOffset = 0;

        foreach (var cmdList in drawData.CmdLists)
        {
            foreach (var cmd in cmdList.CmdBuffer)
            {
                var scissor = new VkRect2D
                {
                    Offset = new VkOffset2D(
                        Int32.Max((Int32)cmd.ClipRect.X, 0),
                        Int32.Max((Int32)cmd.ClipRect.Y, 0)
                    ),
                    Extent = new VkExtent2D(
                        (Int32)(cmd.ClipRect.Z - cmd.ClipRect.X),
                        (Int32)(cmd.ClipRect.W - cmd.ClipRect.Y)
                    )
                };
                context.CommandBuffer.SetScissor(0, [scissor]);

                var textureHandle = new VkDescriptorSet(cmd.GetTexID());
                context.CommandBuffer.BindDescriptorSet(VkPipelineBindPoint.Graphics, pipeline.Layout, 0,
                    textureHandle.IsNotNull() ? textureHandle : _imGuiManager.DescriptorSet, null);

                context.CommandBuffer.DrawIndexed((Int32)cmd.ElemCount, 1, indexOffset + (Int32)cmd.IdxOffset,
                    vertexOffset + (Int32)cmd.VtxOffset, 0);
            }

            vertexOffset += cmdList.VtxBuffer.Count;
            indexOffset += cmdList.IdxBuffer.Count;
        }
    }
}