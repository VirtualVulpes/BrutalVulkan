using Brutal.GlfwApi;

namespace BrutalVulkan;

internal static class Program
{
    private static void Main()
    {
        InitGlfw();
        
        using var app = new App();
        app.Run();
    }
    
    private static void InitGlfw()
    {
        if (!Glfw.Init())
        {
            var errorCode = Glfw.GetError(out var errorMessage);
            throw new InvalidOperationException($"Failed to initialize GLFW: {errorCode}, {errorMessage}");
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_NO_API);
        Glfw.WindowHint(GlfwWindowHint.Resizable, Glfw.Constants.GLFW_TRUE);
    }
}