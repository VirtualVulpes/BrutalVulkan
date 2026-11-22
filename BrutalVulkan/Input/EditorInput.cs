using Brutal.GlfwApi;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.ECS.Systems;
using BrutalVulkan.UI;

namespace BrutalVulkan.Input;

public sealed class EditorInput
{
    private readonly InputManager _input;
    private readonly World _world;
    private readonly NodeSystem _nodeSystem;
    private readonly EntityPicker _picker;
    private readonly SelectionHelper _selection;
    
    public EditorInput(InputManager input, World world, NodeSystem nodeSystem, EntityPicker picker, SelectionHelper selection)
    {
        _input = input;
        _world = world;
        _nodeSystem = nodeSystem;
        _picker = picker;
        _selection = selection;
    }
    
    public void Update()
    {
        var wantMouse = _input.IsImGuiCapturingMouse();
        var wantKeyboard = _input.IsImGuiCapturingKeyboard();
        
        switch (_input.InputMode)
        {
            case InputMode.CameraControl when _input.IsKeyPressed(GlfwKey.Tab):
                _input.SetInputMode(InputMode.UiInteraction);
                break;
            case InputMode.UiInteraction:
            {
                var entity = _picker.HoveredEntity;

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

                    if (_world.IsEntityAlive(entity) && _world.HasComponent<NodeComponent>(entity))
                    {
                        if (_input.IsKeyPressed(GlfwKey.L))
                            _nodeSystem.IncreaseLod(entity);
                        if (_input.IsKeyPressed(GlfwKey.K))
                            _nodeSystem.DecreaseLod(entity);
                    }
                }

                if (!wantMouse)
                {
                    if (_input.IsMouseButtonPressed(GlfwMouseButton.Left))
                        _selection.SelectEntity(entity);
                }

                break;
            }
        }
    }
}