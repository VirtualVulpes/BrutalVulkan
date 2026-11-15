using BrutalVulkan.Rendering;

namespace BrutalVulkan.Materials;

public abstract class Material
{
    public PipelineId PipelineId;

    public abstract void Bind(RenderContext context, Pipeline pipeline);
}