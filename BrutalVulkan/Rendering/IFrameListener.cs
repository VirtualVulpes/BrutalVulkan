namespace BrutalVulkan.Rendering;

public interface IFrameListener
{
    void OnFrameBegin(Int32 frameIndex) { }
    void OnPrepare(RenderContext ctx) { }
    void OnPostRecord(RenderContext ctx) { }
    void OnResize(RenderTargets targets) { }
}