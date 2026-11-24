using Armature.Rendering;
using Armature.Vulkan;
using Brutal;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Pipelines;

public sealed record ComputePipelineOptions
{
    public ByteSize ComputePushSize { get; init; }
    public DescriptorSetLayoutEx[] Layouts { get; init; } = [];
}

public sealed class ComputePipelineBuilder(DeviceEx device, PipelineManager pipelines)
{
    public void Add(string name, string compute, ComputePipelineOptions o)
    {
        var shader = new ShaderCreateInfo { Name = compute, Kind = ShaderKind.ComputeShader };
        var shaderStage = ShaderCreation.CreateShaderStage(shader, device);

        List<VkPushConstantRange> push = [];

        if (o.ComputePushSize != ByteSize.Zero)
        {
            push.Add(
                new VkPushConstantRange
                {
                    Offset = ByteSize.Zero,
                    Size = o.ComputePushSize,
                    StageFlags = VkShaderStageFlags.ComputeBit
                });
        }

        var pipelineDescription = new ComputePipelineDescription
        {
            ShaderStage = shaderStage,
            DescriptorSetLayouts = [.. o.Layouts],
            PushConstantRanges = [.. push]
        };

        pipelines.AddComputePipeline(name, pipelineDescription);
        
        device.DestroyShaderModule(shaderStage.Module, null);
    }
}