using Armature.Rendering;

namespace Armature.Editor;

public interface IEditorTargetConsumer
{
    void OnTargetsRecreated(RenderTargets core, EditorTargets editor);
}