using System.Collections;
using Brutal.GlfwApi;
using Brutal.Numerics;

namespace BrutalVulkan;

public sealed class InputManager : IDisposable
{
    private readonly GlfwWindow _window;
    
    private readonly BitArray _actions = new((int)GlfwKey.Last);
    private readonly BitArray _prevActions = new((int)GlfwKey.Last);

    public double2 MousePos { get; private set; } = double2.Zero;
    public double2 MouseDelta { get; private set; } = double2.Zero;

    public InputManager(GlfwWindow window)
    {
        _window = window;
        
        _window.OnKey += OnKey;
        _window.OnCursorPos += OnCursor;
    }

    private void OnKey(GlfwWindow window, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mod)
    {
        if (key == GlfwKey.Unknown || action == GlfwKeyAction.Repeat) return;
        _actions[(int)key] = (action == GlfwKeyAction.Press);
    }

    private void OnCursor(GlfwWindow window, double2 pos)
    {
        MouseDelta += pos - MousePos;
        MousePos = pos;
    }

    public bool IsKeyDown(GlfwKey key)
    {
        return _actions[(int)key];
    }

    public bool IsKeyPressed(GlfwKey key)
    {
        return _actions[(int)key] && !_prevActions[(int)key];
    }

    public bool IsKeyReleased(GlfwKey key)
    {
        return !_actions[(int)key] && _prevActions[(int)key];
    }

    // call before Glfw.PollEvents()
    public void Update()
    {
        _prevActions.SetAll(false);
        _prevActions.Or(_actions);
        MouseDelta = double2.Zero;
    }

    public void Dispose()
    {
        _window.OnKey -= OnKey;
        _window.OnCursorPos -= OnCursor;
    }
}