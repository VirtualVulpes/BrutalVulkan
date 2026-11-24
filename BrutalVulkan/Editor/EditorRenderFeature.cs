using Armature.Vulkan;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.Passes;
using BrutalVulkan.Rendering.Passes.Compute;

namespace BrutalVulkan.Editor;

public sealed class EditorRenderFeature : IFrameListener, IDisposable
{
    private EditorTargets _targets;
    public readonly IEditorTargetConsumer[] _consumers;

    public EditorRenderFeature(Renderer renderer, VulkanContext ctx, PipelineManager pipelines,
        EditorDescriptors descriptors)
    {
        _targets = new EditorTargets(ctx.Allocator, renderer.Targets.Extent);

        var seed = new SeedInitPass(pipelines.GetPipeline("Seed Init"), descriptors);
        var flood = new JumpFloodPass(pipelines.GetPipeline("Jump Flood"), descriptors);
        var outline = new OutlineCompositePass(pipelines.GetPipeline("Outline Composite"), descriptors);
        _consumers = [seed, flood, outline];
        
        Picker = new EntityPicker(ctx, _targets);

        renderer.AddPass(new EntityIdPass(pipelines.GetPipeline("Entity Id"), _targets), RenderPhase.PostScene);
        renderer.AddPass(new SelectionMaskPass(pipelines.GetPipeline("Selection Mask"), _targets), RenderPhase.PostScene);
        renderer.AddPass(seed, RenderPhase.PostScene);
        renderer.AddPass(flood, RenderPhase.PostScene);
        renderer.AddPass(outline, RenderPhase.PostScene);
        
        renderer.AddListener(this);
        renderer.AddListener(Picker);
        
        Rebind(renderer.Targets);
    }

    public EntityPicker Picker { get; }

    public void OnResize(RenderTargets core)
    {
        _targets.Recreate(core.Extent);
        Rebind(core);
    }

    private void Rebind(RenderTargets core)
    {
        foreach (var c in _consumers) c.OnTargetsRecreated(core, _targets);
    }

    public void Dispose()
    {
        Picker.Dispose();
        _targets.Dispose();
    }
}