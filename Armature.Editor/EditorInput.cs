using Armature.Input;
using Armature.Scene;
using Brutal.GlfwApi;
using Brutal.Numerics;

namespace Armature.Editor;

public sealed class EditorInput
{
    private readonly InputManager _input;
    private readonly World _world;
    private readonly EntityPicker _picker;
    private readonly SelectionHelper _selection;

    public EditorInput(InputManager input, World world, EntityPicker picker,
        SelectionHelper selection)
    {
        _input = input;
        _world = world;
        _picker = picker;
        _selection = selection;
    }

    public void Update()
    {
        var wantMouse = _input.IsUiCapturingMouse();
        var wantKeyboard = _input.IsUiCapturingKeyboard();

        var canPick = _input.InputMode == InputMode.UiInteraction && !wantMouse;
        _picker.Request(canPick ? new int2((Int32)_input.CursorPos.X, (Int32)_input.CursorPos.Y) : null);

        switch (_input.InputMode)
        {
            case InputMode.CameraControl when _input.IsKeyPressed(GlfwKey.Tab):
                _input.SetInputMode(InputMode.UiInteraction);
                break;
            case InputMode.UiInteraction:
                if (!wantKeyboard)
                {
                    if (_input.IsKeyPressed(GlfwKey.Tab))
                    {
                        _input.SetInputMode(InputMode.CameraControl);
                        break;
                    }

                    if (_input.IsKeyPressed(GlfwKey.Delete))
                    {
                        _world.DestroyEntities(_selection.GetSelected());
                        _picker.Reset();
                        break;
                    }
                }

                var hovered = GetHoveredEntity();

                if (!wantMouse && _input.IsMouseButtonPressed(GlfwMouseButton.Left))
                    _selection.Click(hovered, _input.IsKeyDown(GlfwKey.LeftShift));

                break;
        }
    }

    private Entity? GetHoveredEntity() =>
        _picker.HoveredObjectId is { } id
            ? new Entity { Id = new EntityId(id & 0xFFFF), Generation = id >> 16 }
            : null;
}