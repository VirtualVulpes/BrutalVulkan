using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering.Pipelines;

public readonly record struct PipelineId(UInt32 Value)
{
    public static readonly PipelineId Invalid = new(UInt32.MaxValue);
}

public sealed class PipelineManager : IDisposable
{
    private readonly DeviceEx _device;

    private UInt32 _nextId;

    private readonly Dictionary<PipelineId, Pipeline> _pipelines = new();
    private readonly Dictionary<String, PipelineId> _names = new();

    public PipelineManager(DeviceEx device)
    {
        _device = device;
    }

    public PipelineId AddGraphicsPipeline(String name, GraphicsPipelineDescription description)
    {
        var pipelineId = new PipelineId(_nextId++);

        var pipeline = CreateGraphicsPipeline(description, pipelineId);

        _pipelines.Add(pipelineId, pipeline);
        _names.Add(name, pipelineId);

        return pipelineId;
    }

    public PipelineId AddComputePipeline(String name, ComputePipelineDescription description)
    {
        var pipelineId = new PipelineId(_nextId++);

        var pipeline = CreateComputePipeline(description, pipelineId);

        _pipelines.Add(pipelineId, pipeline);
        _names.Add(name, pipelineId);

        return pipelineId;
    }

    public Pipeline GetPipeline(PipelineId pipelineId)
    {
        return _pipelines[pipelineId];
    }

    public Pipeline GetPipeline(String name)
    {
        return _pipelines[_names[name]];
    }

    public PipelineId GetPipelineId(String name)
    {
        return _names[name];
    }

    private unsafe Pipeline CreateGraphicsPipeline(GraphicsPipelineDescription description, PipelineId id)
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
        var colorBlendState = description.ColorBlendStateCreateInfo;

        Span<VkDynamicState> dynamicStates = [VkDynamicState.Viewport, VkDynamicState.Scissor];

        var pipelineLayout = _device.CreatePipelineLayout(description.DescriptorSetLayouts.AsSpan(),
            description.PushConstantRanges.AsSpan(), null);

        fixed (VkDynamicState* dynamicStatesPtr = dynamicStates)
        {
            var dynamicState = new VkPipelineDynamicStateCreateInfo
            {
                DynamicStateCount = dynamicStates.Length,
                DynamicStates = dynamicStatesPtr
            };

            var pipeline = _device.CreateGraphicsPipeline(
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
                    ColorBlendState = &colorBlendState,
                    Layout = pipelineLayout,
                    DynamicState = &dynamicState,
                    DepthStencilState = &depthStencilState
                },
                null
            );

            return new Pipeline(_device, pipeline, pipelineLayout, id);
        }
    }

    private Pipeline CreateComputePipeline(ComputePipelineDescription description, PipelineId id)
    {
        var pipelineLayout = _device.CreatePipelineLayout(description.DescriptorSetLayouts.AsSpan(),
            description.PushConstantRanges.AsSpan(), null);

        var pipeline = _device.CreateComputePipeline(
            VkPipelineCache.NullHandle,
            new VkComputePipelineCreateInfo
            {
                Stage = description.ShaderStage,
                Layout = pipelineLayout
            },
            null);

        return new Pipeline(_device, pipeline, pipelineLayout, id);
    }

    public void Dispose()
    {
        foreach (var pipeline in _pipelines.Values)
        {
            pipeline.Dispose();
        }
    }
}