namespace BrutalVulkan.Rendering;

public interface IRenderPass
{
    public void Record(in RenderContext context, ReadOnlySpan<RenderItem> renderObjects);
}