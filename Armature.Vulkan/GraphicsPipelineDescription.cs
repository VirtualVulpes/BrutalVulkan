using System.Collections.Immutable;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Vulkan;

public readonly record struct GraphicsPipelineDescription(
    VkPipelineRenderingCreateInfo RenderingCreateInfo,
    ShaderStages ShaderStages,
    VertexInput VertexInput,
    VkPipelineRasterizationStateCreateInfo RasterizationStateCreateInfo,
    VkPipelineMultisampleStateCreateInfo MultisampleStateCreateInfo,
    VkPipelineDepthStencilStateCreateInfo DepthStencilStateCreateInfo,
    ImmutableArray<VkDescriptorSetLayout> DescriptorSetLayouts,
    ImmutableArray<VkPushConstantRange> PushConstantRanges,
    VkPipelineColorBlendStateCreateInfo ColorBlendStateCreateInfo
);