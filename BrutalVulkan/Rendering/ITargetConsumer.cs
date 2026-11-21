namespace BrutalVulkan.Rendering;

public interface ITargetConsumer
{
    void OnTargetsRecreated(RenderTargets targets);
}