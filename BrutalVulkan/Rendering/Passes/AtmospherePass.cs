using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Rendering.Passes;

public class AtmospherePass : IRenderPass, ITargetConsumer
{
    private readonly Pipeline _pipeline;
    private readonly DescriptorManager _descriptors;
    private readonly VkDescriptorSet _descriptorSet;
    
    public AtmospherePass(Pipeline pipeline, DescriptorManager descriptors)
    {
        _pipeline = pipeline;
        _descriptors = descriptors;
        _descriptorSet = descriptors.AllocateAtmosphereDescriptor();
    }

    public unsafe void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects)
    {
        context.Targets.Resolve.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit);
        
        context.Targets.EntityIdDepth.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.ShaderReadBit, VkImageAspectFlags.DepthBit);

        context.Targets.PostProcess.Image.TransitionTo(context.CommandBuffer,
            VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags.ColorAttachmentOutputBit, VkAccessFlags.ColorAttachmentWriteBit);
        
        var colorAttachment = new VkRenderingAttachmentInfo
        {
            ImageView = context.Targets.PostProcess.View.VkImageView,
            ImageLayout = VkImageLayout.ColorAttachmentOptimal,
            LoadOp = VkAttachmentLoadOp.Clear,
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
                [context.FrameResources.PerFrameDescriptorSet, _descriptorSet],
                null);

        var a = context.Scene.Atmosphere;
        var pushConsts = new PushConstantBlocks.AtmosphereBlock
        {
            Center = a.Center,
            Radius = a.Radius,
            LightDirection = a.LightDirection,
            Enabled = a.Enabled
        };
        
        context.CommandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.FragmentBit, ByteSize.Zero, pushConsts);

        context.CommandBuffer.Draw(3, 1, 0, 0);

        context.CommandBuffer.EndRendering();
    }
    
    public void OnTargetsRecreated(RenderTargets targets)
    {
        _descriptors.UpdateAtmosphereDescriptor(_descriptorSet, targets.Resolve.View.VkImageView, targets.EntityIdDepth.View.VkImageView);
    }
}