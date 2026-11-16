using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan.Utility;

public static class ShaderCreation
{
    public static unsafe ShaderStages CreateShaderStages(ReadOnlySpan<ShaderCreateInfo> shaders, DeviceEx device)
    {
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();

        var stages = new List<VkPipelineShaderStageCreateInfo>();

        foreach (var shader in shaders)
        {
            var name = LoadShader(shader.Name);
            using var spirv = compiler.Compile(name, shader.Kind, name, "main"u8, options);
            var module = ShaderModuleExtensions.FromSpirv(
                device,
                new ReadOnlySpan<Byte>(spirv.GetBytes(), spirv.GetLength()),
                null
            );

            switch (shader.Kind)
            {
                case ShaderKind.VertexShader:
                    stages.Add(Presets.ShaderStage.Vertex(module));
                    break;
                case ShaderKind.FragmentShader:
                    stages.Add(Presets.ShaderStage.Fragment(module));
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported ShaderKind: {shader.Kind}");
            }
        }

        return new ShaderStages(device, [.. stages], null);
    }

    private static Byte[] LoadShader(String filename)
    {
        var path = Path.Join("shaders", filename);

        return File.ReadAllBytes(path);
    }
}