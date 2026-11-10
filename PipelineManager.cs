using System.Collections.Immutable;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan;

public readonly record struct PipelineId(uint value);

public readonly record struct PipelineDescription(
    PipelineId Id,
    VkPipelineRenderingCreateInfo RenderingCreateInfo,
    ShaderStages ShaderStages,
    VertexInput VertexInput,
    VkPipelineRasterizationStateCreateInfo RasterizationStateCreateInfo,
    VkPipelineMultisampleStateCreateInfo MultisampleStateCreateInfo,
    VkPipelineDepthStencilStateCreateInfo DepthStencilStateCreateInfo,
    ImmutableArray<VkDescriptorSetLayout> DescriptorSetLayouts,
    ImmutableArray<VkPushConstantRange> PushConstantRanges
);

public sealed class PipelineManager : IDisposable
{
    private readonly DeviceEx _device;

    private readonly Dictionary<PipelineId, Pipeline> _pipelines = new();

    public PipelineManager(DeviceEx device)
    {
        _device = device;
    }

    public Pipeline GetOrCreate(PipelineDescription description)
    {
        if (_pipelines.TryGetValue(description.Id, out Pipeline pipeline)) return pipeline;

        pipeline = CreatePipeline(description);

        _pipelines.Add(description.Id, pipeline);

        return pipeline;
    }

    private unsafe Pipeline CreatePipeline(PipelineDescription description)
    {
        var viewportState = new VkPipelineViewportStateCreateInfo
        {
            ViewportCount = 1,
            ScissorCount = 1,
        };

        var renderingCreateInfo = description.RenderingCreateInfo;
        var rasterizationState = description.RasterizationStateCreateInfo;
        var multisampleState = description.MultisampleStateCreateInfo;
        var depthStencilState = description.DepthStencilStateCreateInfo;

        Span<VkDynamicState> dynamicStates = [VkDynamicState.Viewport, VkDynamicState.Scissor];

        var pipelineLayout = _device.CreatePipelineLayout(description.DescriptorSetLayouts.AsSpan(), description.PushConstantRanges.AsSpan(), null);

        fixed (VkDynamicState* dynamicStatesPtr = dynamicStates)
        {
            var dynamicState = new VkPipelineDynamicStateCreateInfo
            {
                DynamicStateCount = dynamicStates.Length,
                DynamicStates = dynamicStatesPtr
            };

            VkPipeline pipeline = _device.CreateGraphicsPipeline(
                VkPipelineCache.NullHandle,
                new VkGraphicsPipelineCreateInfo
                {
                    Next = &renderingCreateInfo,
                    StageCount = description.ShaderStages.Count,
                    Stages = description.ShaderStages,
                    VertexInputState = description.VertexInput,
                    InputAssemblyState = Presets.InputAssembly.TriangleList,
                    ViewportState = &viewportState,
                    RasterizationState = &rasterizationState,
                    MultisampleState = &multisampleState,
                    ColorBlendState = Presets.BlendState.BlendNone,
                    Layout = pipelineLayout,
                    DynamicState = &dynamicState,
                    DepthStencilState = &depthStencilState
                },
                null
            );

            return new Pipeline(_device, pipeline, pipelineLayout);
        }
    }

    public void Dispose()
    {
        foreach (var pipeline in _pipelines.Values)
        {
            pipeline.Dispose();
        }
    }
}