using System.Collections;
using Brutal.GlfwApi;
using Brutal.Numerics;

namespace Armature.Input;

public enum InputMode
{
    CameraControl,
    UiInteraction
}

public sealed class InputManager : IDisposable
{
    private readonly GlfwWindow _window;

    private readonly BitArray _keyActions = new((int)GlfwKey.Last);
    private readonly BitArray _prevKeyActions = new((int)GlfwKey.Last);

    private readonly BitArray _mouseActions = new((int)GlfwMouseButton.Last);
    private readonly BitArray _prevMouseActions = new((int)GlfwMouseButton.Last);

    private bool _resetCursorDelta;
    
    public double2 CursorPos { get; private set; } = double2.Zero;
    public double2 CursorDelta { get; private set; } = double2.Zero;

    public InputMode InputMode { get; private set; } = InputMode.CameraControl;
    
    public IUiCaptureState? Ui { get; set; }
    public event Action<InputMode, double2>? InputModeChanged;

    public InputManager(GlfwWindow window)
    {
        _window = window;

        _window.OnKey += OnKey;
        _window.OnCursorPos += OnCursorPos;
        _window.OnMouseButton += OnMouseButton;
    }

    private void OnKey(GlfwWindow window, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mod)
    {
        if (key == GlfwKey.Unknown || action == GlfwKeyAction.Repeat) return;
        _keyActions[(int)key] = (action == GlfwKeyAction.Press);
    }

    private void OnCursorPos(GlfwWindow window, double2 pos)
    {
        var scaledPos = pos * new double2(window.ContentScale.X, window.ContentScale.Y);
        
        if (_resetCursorDelta)
        {
            CursorDelta = double2.Zero;
            _resetCursorDelta = false;
        }
        else
        {
            CursorDelta += scaledPos - CursorPos;
        }
        CursorPos = scaledPos;
    }

    private void OnMouseButton(GlfwWindow window, GlfwMouseButton button, GlfwButtonAction action, GlfwModifier mods)
    {
        _mouseActions[(int)button] = (action == GlfwButtonAction.Press);
    }

    public bool IsKeyDown(GlfwKey key)
    {
        return _keyActions[(int)key];
    }

    public bool IsKeyPressed(GlfwKey key)
    {
        return _keyActions[(int)key] && !_prevKeyActions[(int)key];
    }

    public bool IsKeyReleased(GlfwKey key)
    {
        return !_keyActions[(int)key] && _prevKeyActions[(int)key];
    }

    public bool IsMouseButtonPressed(GlfwMouseButton button)
    {
        return _mouseActions[(int)button] && !_prevMouseActions[(int)button];
    }

    // call before Glfw.PollEvents()
    public void Update()
    {
        _prevKeyActions.SetAll(false);
        _prevKeyActions.Or(_keyActions);
        
        _prevMouseActions.SetAll(false);
        _prevMouseActions.Or(_mouseActions);
        CursorDelta = double2.Zero;
    }

    public void SetInputMode(InputMode inputMode)
    {
        switch (inputMode)
        {
            case InputMode.CameraControl:
                Glfw.PInvoke.SetInputMode(_window, (int)GlfwInputMode.Cursor, (int)GlfwCursorMode.Disabled);
                break;
            case InputMode.UiInteraction:
                Glfw.PInvoke.SetInputMode(_window, (int)GlfwInputMode.Cursor, (int)GlfwCursorMode.Normal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inputMode), inputMode, null);
        }
        
        Glfw.PInvoke.GetCursorPos(_window, out var x, out var y);
        InputModeChanged?.Invoke(inputMode, new double2(x, y));
        
        InputMode = inputMode;

        _resetCursorDelta = true;
    }

    public void Dispose()
    {
        _window.OnKey -= OnKey;
        _window.OnCursorPos -= OnCursorPos;
        _window.OnMouseButton -= OnMouseButton;
    }

    public bool IsUiCapturingKeyboard() => Ui?.WantsKeyboard ?? false;
    public bool IsUiCapturingMouse() => Ui?.WantsMouse ?? false;
}