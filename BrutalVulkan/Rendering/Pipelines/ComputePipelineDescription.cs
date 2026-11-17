using System.Collections.Immutable;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering.Pipelines;

public readonly record struct ComputePipelineDescription
(
    VkPipelineShaderStageCreateInfo ShaderStage,
    ImmutableArray<VkDescriptorSetLayout> DescriptorSetLayouts,
    ImmutableArray<VkPushConstantRange> PushConstantRanges
);