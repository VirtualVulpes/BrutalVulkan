namespace Armature.Input;

public interface IUiCaptureState
{
    bool WantsKeyboard { get; }
    bool WantsMouse { get; }
}