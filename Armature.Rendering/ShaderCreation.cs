using Armature.Rendering.Pipelines;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Rendering;

public static class ShaderCreation
{
    public static unsafe ShaderStages CreateShaderStages(ReadOnlySpan<ShaderCreateInfo> shaders, DeviceEx device)
    {
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();

        var stages = new List<VkPipelineShaderStageCreateInfo>();

        foreach (var shader in shaders)
        {
            var name = LoadShader(shader);
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
    
    public static unsafe VkPipelineShaderStageCreateInfo CreateShaderStage(ShaderCreateInfo shader, DeviceEx device)
    {
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();
        
        var name = LoadShader(shader);
        using var spirv = compiler.Compile(name, shader.Kind, name, "main"u8, options);
        var module = ShaderModuleExtensions.FromSpirv(
            device,
            new ReadOnlySpan<Byte>(spirv.GetBytes(), spirv.GetLength()),
            null
        );

        switch (shader.Kind)
        {
            case ShaderKind.VertexShader:
                return Presets.ShaderStage.Vertex(module);
            case ShaderKind.FragmentShader:
                return Presets.ShaderStage.Fragment(module);
            case ShaderKind.ComputeShader:
                return Presets.ShaderStage.Compute(module);
            default:
                throw new ArgumentOutOfRangeException($"Unsupported ShaderKind: {shader.Kind}");
        }
    }

    private static Byte[] LoadShader(ShaderCreateInfo shader)
    {
        var folder = shader.Kind switch
        {
            ShaderKind.VertexShader => "vertex",
            ShaderKind.FragmentShader => "fragment",
            ShaderKind.ComputeShader => "compute",
            _ => throw new ArgumentOutOfRangeException($"Unsupported ShaderKind: {shader.Kind}")
        };
        var path = Path.Combine("shaders", folder, shader.Name);

        return File.ReadAllBytes(path);
    }
}