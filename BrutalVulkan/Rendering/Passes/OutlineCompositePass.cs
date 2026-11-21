using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public class OutlineCompositePass : IRenderPass, ITargetConsumer
{
    private Pipeline _pipeline;
    private DescriptorManager _descriptors;
    private VkDescriptorSet _descriptorSet;

    public OutlineCompositePass(Pipeline pipeline, DescriptorManager descriptors)
    {
        _pipeline = pipeline;
        _descriptors = descriptors;
        _descriptorSet = descriptors.AllocateOutlineDescriptorSet();
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.Targets.PostProcess.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);

        context.Targets.SeedA.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.General, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);

        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.SwapchainImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Load,
            StoreOp = VkAttachmentStoreOp.Store,
            ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
        };

        context.CommandBuffer.BeginRendering(
            new VkRenderingInfo
            {
                RenderArea = new VkRect2D(context.Targets.Extent),
                LayerCount = 1,
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment
            }
        );

        context.CommandBuffer.SetViewport(0, [
            new VkViewport
            {
                X = 0.0f,
                Y = context.Targets.Extent.Height,
                Width = context.Targets.Extent.Width,
                Height = -context.Targets.Extent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            }
        ]);
        context.CommandBuffer.SetScissor(0, [new VkRect2D(context.Targets.Extent)]);

        context.CommandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);
        context.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0,
                [_descriptorSet], null);

        context.CommandBuffer.Draw(3, 1, 0, 0);

        context.CommandBuffer.EndRendering();
    }

    public void OnTargetsRecreated(RenderTargets targets)
    {
        _descriptors.UpdateOutlineDescriptor(_descriptorSet,
            targets.PostProcess.View.VkImageView, targets.SeedA.View.VkImageView);
    }
}