using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.UI;

namespace BrutalVulkan.Rendering.Passes;

public class ImGuiPass : IRenderPass
{
    private readonly ImGuiManager _manager;

    public ImGuiPass(ImGuiManager imGuiManager)
    {
        _manager = imGuiManager;
    }

    public unsafe void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> _)
    {
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = ctx.SwapchainImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Load,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
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

        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0)
        {
            ctx.CommandBuffer.EndRendering();
            return;
        }

        var fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fbWidth <= 0 || fbHeight <= 0)
        {
            ctx.CommandBuffer.EndRendering();
            return;
        }

        var cmdBuf = ctx.CommandBuffer;

        cmdBuf.BindPipeline(VkPipelineBindPoint.Graphics, _manager.Pipeline.Handle);

        var viewport = new VkViewport
        {
            Width = fbWidth,
            Height = fbHeight,
            MinDepth = 0.0f,
            MaxDepth = 1.0f,
        };
        cmdBuf.SetViewport(0, [viewport]);

        var scale = new float2(2.0f / drawData.DisplaySize.X, 2.0f / drawData.DisplaySize.Y);
        var pushConsts = new PushConstantBlocks.ImGuiBlock
        {
            Scale = scale,
            Translate = new float2(
                -1.0f - drawData.DisplayPos.X * scale.X,
                -1.0f - drawData.DisplayPos.Y * scale.Y
            )
        };
        cmdBuf.PushConstants(_manager.Pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero, pushConsts);

        cmdBuf.BindVertexBuffer(0, _manager.GetVertexBuffer(ctx.FrameIndex), ByteSize.Zero);
        cmdBuf.BindIndexBuffer(_manager.GetIndexBuffer(ctx.FrameIndex), VkIndexType.UInt16);

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
                        (int)minX,
                        (int)minY
                    ),
                    Extent = new VkExtent2D(
                        (int)(maxX - minX),
                        (int)(maxY - minY)
                    )
                };
                cmdBuf.SetScissor(0, [scissor]);

                var set = new VkDescriptorSet(cmd.GetTexID());
                if (set.IsNull()) throw new InvalidOperationException("Descriptor set should not be null");

                if (set.VkHandle != lastSet.VkHandle)
                {
                    cmdBuf.BindDescriptorSet(VkPipelineBindPoint.Graphics, _manager.Pipeline.Layout, 0, set, null);
                    lastSet = set;
                }

                cmdBuf.DrawIndexed((int)cmd.ElemCount, 1, indexOffset + (int)cmd.IdxOffset,
                    vertexOffset + (int)cmd.VtxOffset, 0);
            }

            vertexOffset += cmdList.VtxBuffer.Count;
            indexOffset += cmdList.IdxBuffer.Count;
        }

        ctx.CommandBuffer.EndRendering();
    }
}