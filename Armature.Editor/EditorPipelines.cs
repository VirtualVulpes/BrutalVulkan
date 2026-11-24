using Armature.Rendering;
using Armature.Rendering.Pipelines;
using Armature.Vulkan;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Editor;

public static class EditorPipelines
{
    public static void Register(DeviceEx device, PipelineManager pipelines, PipelineBuildContext ctx, CoreDescriptors core, EditorDescriptors editor)
    {
        var m = new MeshPipelineBuilder(device, pipelines);
        var c = new ComputePipelineBuilder(device, pipelines);

        m.Add("Selection Mask", "world_space.vert", "selection_mask.frag", new MeshPipelineOptions
        {
            ColorFormat = VkFormat.R8UNorm,
            FragmentPushSize = ByteSize.Of<PushConstantBlocks.TextureFragmentBlock>(),
            Layouts = [core.GlobalLayout, core.PerFrameLayout]
        });

        m.Add("Entity Id", "world_space.vert", "entity_id.frag", new MeshPipelineOptions
        {
            ColorFormat = VkFormat.R32UInt,
            FragmentPushSize = ByteSize.Of<PushConstantBlocks.EntityIdBlock>()
        });
        
        m.Add("Outline Composite", "fullscreen.vert", "outline_composite.frag", new MeshPipelineOptions
        {
            ColorFormat = ctx.ColorFormat,
            FragmentPushSize = ByteSize.Zero,
            Layouts = [editor.OutlineCompositeLayout]
        });
        
        c.Add("Seed Init", "seed_init.comp", new ComputePipelineOptions
        {
            ComputePushSize = ByteSize.Zero,
            Layouts = [editor.SeedLayout]
        });
        
        c.Add("Jump Flood", "jump_flood.comp", new ComputePipelineOptions
        {
            ComputePushSize = ByteSize.Of<uint>(),
            Layouts = [editor.SeedLayout]
        });
    }
}