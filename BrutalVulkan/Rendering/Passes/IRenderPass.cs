namespace BrutalVulkan.Rendering.Passes;

public interface IRenderPass
{
    public void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects);
}