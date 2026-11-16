using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public class ImGuiPass : IRenderPass
{
    private readonly ImGuiManager _manager;
    private readonly Pipeline _pipeline;

    public ImGuiPass(ImGuiManager imGuiManager, Pipeline imGuiPipeline)
    {
        _manager = imGuiManager;
        _pipeline = imGuiPipeline;
    }

    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> _)
    {
        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0) return;

        var fbWidth = (Int32)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var fbHeight = (Int32)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0) return;

        var cmdBuf = context.CommandBuffer;

        cmdBuf.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);

        var viewport = new VkViewport
        {
            Width = fbWidth,
            Height = fbHeight,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };
        cmdBuf.SetViewport(0, [viewport]);

        var scale = new float2(2.0f / drawData.DisplaySize.X, 2.0f / drawData.DisplaySize.Y);
        var pushConsts = new PushConstantBlocks.ImGui
        {
            Scale = scale,
            Translate = new float2(
                -1.0f - drawData.DisplayPos.X * scale.X,
                -1.0f - drawData.DisplayPos.Y * scale.Y
            )
        };
        cmdBuf.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero, pushConsts);

        cmdBuf.BindVertexBuffer(0, _manager.GetVertexBuffer(context.FrameIndex), ByteSize.Zero);
        cmdBuf.BindIndexBuffer(_manager.GetIndexBuffer(context.FrameIndex), VkIndexType.UInt16);

        var clipOff = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;

        var vertexOffset = 0;
        var indexOffset = 0;
        var lastSet = default(VkDescriptorSet);

        foreach (var cmdList in drawData.CmdLists)
        {
            foreach (var cmd in cmdList.CmdBuffer)
            {
                var minX = (cmd.ClipRect.X - clipOff.X) * clipScale.X;
                var minY = (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y;
                var maxX = (cmd.ClipRect.Z - clipOff.X) * clipScale.X;
                var maxY = (cmd.ClipRect.W - clipOff.Y) * clipScale.Y;

                if (minX < 0) minX = 0;
                if (minY < 0) minY = 0;
                if (maxX > fbWidth) maxX = fbWidth;
                if (maxY > fbHeight) maxY = fbHeight;

                if (maxX <= minX || maxY <= minY) continue;

                var scissor = new VkRect2D
                {
                    Offset = new VkOffset2D(
                        (Int32)minX,
                        (Int32)minY
                    ),
                    Extent = new VkExtent2D(
                        (Int32)(maxX - minX),
                        (Int32)(maxY - minY)
                    )
                };
                cmdBuf.SetScissor(0, [scissor]);

                var set = new VkDescriptorSet(cmd.GetTexID());
                if (set.IsNull()) throw new InvalidOperationException("Descriptor set should not be null");

                if (set.VkHandle != lastSet.VkHandle)
                {
                    cmdBuf.BindDescriptorSet(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0, set, null);
                    lastSet = set;
                }

                cmdBuf.DrawIndexed((Int32)cmd.ElemCount, 1, indexOffset + (Int32)cmd.IdxOffset,
                    vertexOffset + (Int32)cmd.VtxOffset, 0);
            }

            vertexOffset += cmdList.VtxBuffer.Count;
            indexOffset += cmdList.IdxBuffer.Count;
        }
    }
}