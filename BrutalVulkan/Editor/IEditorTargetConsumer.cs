using BrutalVulkan.Rendering;

namespace BrutalVulkan.Editor;

public interface IEditorTargetConsumer
{
    void OnTargetsRecreated(RenderTargets core, EditorTargets editor);
}