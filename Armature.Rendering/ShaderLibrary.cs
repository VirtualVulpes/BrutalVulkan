using Armature.Rendering.Pipelines;
using Brutal.ShaderCApi;

namespace Armature.Rendering;

public static class ShaderLibrary
{
    private static readonly List<string> Roots = [Path.Combine(AppContext.BaseDirectory, "shaders")];

    public static void AddRoot(string directory) => Roots.Add(directory);

    internal static Byte[] Load(ShaderCreateInfo shader)
    {
        var folder = shader.Kind switch
        {
            ShaderKind.VertexShader => "vertex",
            ShaderKind.FragmentShader => "fragment",
            ShaderKind.ComputeShader => "compute",
            _ => throw new ArgumentOutOfRangeException($"Unsupported ShaderKind: {shader.Kind}")
        };

        foreach (var root in Roots)
        {
            var path = Path.Combine(root, folder, shader.Name);
            if (File.Exists(path)) return File.ReadAllBytes(path);
        }

        throw new FileNotFoundException($"Shader '{shader.Name}' not found in any registered root.");
    }

}