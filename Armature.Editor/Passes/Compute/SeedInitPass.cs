using Armature.Rendering;
using Armature.Rendering.Passes;
using Armature.SceneRendering;
using Armature.Vulkan;
using Brutal.VulkanApi;

namespace Armature.Editor.Passes.Compute;

public class SeedInitPass : IRenderPass, IEditorTargetConsumer
{
    private readonly Pipeline _pipeline;
    private EditorDescriptors _editor;
    private readonly VkDescriptorSet _descriptorSet;
    private EditorTargets _targets;

    public SeedInitPass(Pipeline pipeline, EditorDescriptors editor)
    {
        _pipeline = pipeline;
        _editor = editor;
        _descriptorSet = editor.AllocateSeedDescriptorSet();
    }

    public void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> _)
    {
        _targets.SelectionMask.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderReadBit);

        _targets.SeedA.Image.TransitionTo(ctx.CommandBuffer, VkImageLayout.General,
            VkPipelineStageFlags.ComputeShaderBit, VkAccessFlags.ShaderWriteBit);

        ctx.CommandBuffer.BindPipeline(VkPipelineBindPoint.Compute, _pipeline.Handle);
        ctx.CommandBuffer
            .BindDescriptorSets(VkPipelineBindPoint.Compute, _pipeline.Layout, 0,
                [_descriptorSet],
                null);

        const Int32 localSizeX = 16;
        const Int32 localSizeY = 16;

        ctx.CommandBuffer.Dispatch((ctx.Targets.Extent.Width + localSizeX - 1) / localSizeX,
            (ctx.Targets.Extent.Height + localSizeY - 1) / localSizeY, 1);
    }

    public void OnTargetsRecreated(RenderTargets core, EditorTargets editor)
    {
        _targets = editor;   
        _editor.UpdateSeedDescriptor(_descriptorSet, editor.SelectionMask.View.VkImageView,
            editor.SeedA.View.VkImageView);
    }
}