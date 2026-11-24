using Brutal.GlfwApi;
using Brutal.ImGuiApi;

namespace Armature.DebugUi;

public static class GlfwImGuiTranslation
{
    public static ImGuiKey ToImGuiKey(GlfwKey key)
    {
        return key switch
        {
            >= GlfwKey.A and <= GlfwKey.Z => ImGuiKey.A + (key - GlfwKey.A),
            >= GlfwKey.Number0 and <= GlfwKey.Number9 => ImGuiKey._0 + (key - GlfwKey.Number0),
            >= GlfwKey.F1 and <= GlfwKey.F24 => ImGuiKey.F1 + (key - GlfwKey.F1),
            >= GlfwKey.Kp0 and <= GlfwKey.KpEqual => ImGuiKey.Keypad0 + (key - GlfwKey.Kp0),
            _ => key switch
            {
                GlfwKey.Tab => ImGuiKey.Tab,
                GlfwKey.Left => ImGuiKey.LeftArrow,
                GlfwKey.Right => ImGuiKey.RightArrow,
                GlfwKey.Up => ImGuiKey.UpArrow,
                GlfwKey.Down => ImGuiKey.DownArrow,
                GlfwKey.PageUp => ImGuiKey.PageUp,
                GlfwKey.PageDown => ImGuiKey.PageDown,
                GlfwKey.Home => ImGuiKey.Home,
                GlfwKey.End => ImGuiKey.End,
                GlfwKey.Insert => ImGuiKey.Insert,
                GlfwKey.Delete => ImGuiKey.Delete,
                GlfwKey.Backspace => ImGuiKey.Backspace,
                GlfwKey.Space => ImGuiKey.Space,
                GlfwKey.Enter => ImGuiKey.Enter,
                GlfwKey.Escape => ImGuiKey.Escape,
                GlfwKey.LeftShift => ImGuiKey.LeftShift,
                GlfwKey.RightShift => ImGuiKey.RightShift,
                GlfwKey.LeftControl => ImGuiKey.LeftCtrl,
                GlfwKey.RightControl => ImGuiKey.RightCtrl,
                GlfwKey.LeftAlt => ImGuiKey.LeftAlt,
                GlfwKey.RightAlt => ImGuiKey.RightAlt,
                GlfwKey.LeftSuper => ImGuiKey.LeftSuper,
                GlfwKey.RightSuper => ImGuiKey.RightSuper,
                GlfwKey.Minus => ImGuiKey.Minus,
                GlfwKey.Equal => ImGuiKey.Equal,
                GlfwKey.Comma => ImGuiKey.Comma,
                GlfwKey.Period => ImGuiKey.Period,
                GlfwKey.Slash => ImGuiKey.Slash,
                GlfwKey.Semicolon => ImGuiKey.Semicolon,
                GlfwKey.Apostrophe => ImGuiKey.Apostrophe,
                GlfwKey.LeftBracket => ImGuiKey.LeftBracket,
                GlfwKey.RightBracket => ImGuiKey.RightBracket,
                GlfwKey.Backslash => ImGuiKey.Backslash,
                GlfwKey.GraveAccent => ImGuiKey.GraveAccent,
                GlfwKey.CapsLock => ImGuiKey.CapsLock,
                GlfwKey.ScrollLock => ImGuiKey.ScrollLock,
                GlfwKey.NumLock => ImGuiKey.NumLock,
                GlfwKey.PrintScreen => ImGuiKey.PrintScreen,
                GlfwKey.Pause => ImGuiKey.Pause,
                GlfwKey.Menu => ImGuiKey.Menu,
                _ => ImGuiKey.None
            }
        };
    }
}