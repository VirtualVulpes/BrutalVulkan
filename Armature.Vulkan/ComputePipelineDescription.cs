using System.Collections.Immutable;
using Brutal.VulkanApi;

namespace Armature.Vulkan;

public readonly record struct ComputePipelineDescription
(
    VkPipelineShaderStageCreateInfo ShaderStage,
    ImmutableArray<VkDescriptorSetLayout> DescriptorSetLayouts,
    ImmutableArray<VkPushConstantRange> PushConstantRanges
);