using System.Collections;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace BrutalVulkan.Input;

public enum InputMode
{
    CameraControl,
    UiInteraction
}

public sealed class InputManager : IDisposable
{
    private readonly GlfwWindow _window;

    private readonly BitArray _keyActions = new((Int32)GlfwKey.Last);
    private readonly BitArray _prevKeyActions = new((Int32)GlfwKey.Last);

    private readonly BitArray _mouseActions = new((Int32)GlfwMouseButton.Last);
    private readonly BitArray _prevMouseActions = new((Int32)GlfwMouseButton.Last);

    private Boolean _resetCursorDelta;
    
    public double2 CursorPos { get; private set; } = double2.Zero;
    public double2 CursorDelta { get; private set; } = double2.Zero;

    public InputMode InputMode { get; private set; } = InputMode.CameraControl;

    public InputManager(GlfwWindow window)
    {
        _window = window;

        _window.OnKey += OnKey;
        _window.OnCursorPos += OnCursorPos;
        _window.OnMouseButton += OnMouseButton;
    }

    private void OnKey(GlfwWindow window, GlfwKey key, Int32 scancode, GlfwKeyAction action, GlfwModifier mod)
    {
        if (key == GlfwKey.Unknown || action == GlfwKeyAction.Repeat) return;
        _keyActions[(Int32)key] = (action == GlfwKeyAction.Press);
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
        _mouseActions[(Int32)button] = (action == GlfwButtonAction.Press);
    }

    public Boolean IsKeyDown(GlfwKey key)
    {
        return _keyActions[(Int32)key];
    }

    public Boolean IsKeyPressed(GlfwKey key)
    {
        return _keyActions[(Int32)key] && !_prevKeyActions[(Int32)key];
    }

    public Boolean IsKeyReleased(GlfwKey key)
    {
        return !_keyActions[(Int32)key] && _prevKeyActions[(Int32)key];
    }

    public Boolean IsMouseButtonPressed(GlfwMouseButton button)
    {
        return _mouseActions[(Int32)button] && !_prevMouseActions[(Int32)button];
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
        var io = ImGui.GetIO();
        switch (inputMode)
        {
            case InputMode.CameraControl:
                Glfw.PInvoke.SetInputMode(_window, (Int32)GlfwInputMode.Cursor, (Int32)GlfwCursorMode.Disabled);
                io.ConfigFlags |= ImGuiConfigFlags.NoMouse | ImGuiConfigFlags.NoMouseCursorChange;
                io.AddMousePosEvent(-Single.MaxValue, -Single.MaxValue);
                break;
            case InputMode.UiInteraction:
                Glfw.PInvoke.SetInputMode(_window, (Int32)GlfwInputMode.Cursor, (Int32)GlfwCursorMode.Normal);
                io.ConfigFlags &= ~(ImGuiConfigFlags.NoMouse | ImGuiConfigFlags.NoMouseCursorChange);
                
                Glfw.PInvoke.GetCursorPos(_window, out var x, out var y);
                io.AddMousePosEvent((Single)x, (Single)y);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(inputMode), inputMode, null);
        }
        
        InputMode = inputMode;

        _resetCursorDelta = true;
    }

    public void Dispose()
    {
        _window.OnKey -= OnKey;
        _window.OnCursorPos -= OnCursorPos;
        _window.OnMouseButton -= OnMouseButton;
    }

    public Boolean IsImGuiCapturingKeyboard() => ImGui.GetIO().WantCaptureKeyboard;
    public Boolean IsImGuiCapturingMouse() => ImGui.GetIO().WantCaptureMouse;
}