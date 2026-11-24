namespace Armature.Rendering.Passes;

public interface IRenderPass
{
    public void Record(in RenderContext ctx, ReadOnlySpan<RenderItem> renderObjects);
}