using Armature.Rendering;
using Armature.Rendering.Passes;
using Armature.Vulkan;
using Brutal.Numerics;
using Brutal.VulkanApi;

namespace Armature.Editor.Passes;

public class OutlineCompositePass : IRenderPass, IEditorTargetConsumer
{
    private Pipeline _pipeline;
    private EditorDescriptors _editor;
    private VkDescriptorSet _descriptorSet;
    private EditorTargets _targets;

    public OutlineCompositePass(Pipeline pipeline, EditorDescriptors editor)
    {
        _pipeline = pipeline;
        _editor = editor;
        _descriptorSet = editor.AllocateOutlineDescriptorSet();
    }

    public unsafe void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects)
    {
        ctx.Targets.ResolvedColor.Image.TransitionTo(ctx.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);

        _targets.SeedA.Image.TransitionTo(ctx.CommandBuffer,
            VkImageLayout.General, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);

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
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0,
                [_descriptorSet], null);

        ctx.CommandBuffer.Draw(3, 1, 0, 0);

        ctx.CommandBuffer.EndRendering();
    }

    public void OnTargetsRecreated(RenderTargets core, EditorTargets editor)
    {
        _targets = editor;   
        _editor.UpdateOutlineDescriptor(_descriptorSet,
            core.ResolvedColor.View.VkImageView, editor.SeedA.View.VkImageView);
    }
}