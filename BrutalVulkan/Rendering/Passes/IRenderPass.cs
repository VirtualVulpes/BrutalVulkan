namespace BrutalVulkan.Rendering.Passes;

public interface IRenderPass
{
    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects);
}