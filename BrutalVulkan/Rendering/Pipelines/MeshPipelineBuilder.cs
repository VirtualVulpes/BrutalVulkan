using Armature.Rendering;
using Armature.Vulkan;
using Brutal;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Pipelines;

public sealed record MeshPipelineOptions
{
    public VkFormat ColorFormat { get; init; }
    public VkSampleCountFlags Samples { get; init; }= VkSampleCountFlags._1Bit;
    public VkPolygonMode PolygonMode { get; init; }= VkPolygonMode.Fill;
    public ByteSize FragmentPushSize { get; init; }
    public DescriptorSetLayoutEx[] Layouts { get; init; } = [];
}

public sealed class MeshPipelineBuilder(DeviceEx device, PipelineManager pipelines)
{
    public unsafe void Add(string name, string vert, string frag, MeshPipelineOptions o)
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = vert, Kind = ShaderKind.VertexShader },
            new() { Name = frag, Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = o.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        var depthStencil = new VkPipelineDepthStencilStateCreateInfo
        {
            DepthTestEnable = true,
            DepthWriteEnable = true,
            DepthCompareOp = VkCompareOp.Less,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false
        };

        var multisampleState = new VkPipelineMultisampleStateCreateInfo
        {
            RasterizationSamples = o.Samples
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = o.PolygonMode,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };
        
        List<VkPushConstantRange> push =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            }
        ];

        if (o.FragmentPushSize != ByteSize.Zero)
        {
            push.Add(
                new VkPushConstantRange
                {
                    Offset = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                    Size = o.FragmentPushSize,
                    StageFlags = VkShaderStageFlags.FragmentBit
                });
        }
        
        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [.. o.Layouts],
            PushConstantRanges = [.. push],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        pipelines.AddGraphicsPipeline(name, pipelineDescription);
    }
}