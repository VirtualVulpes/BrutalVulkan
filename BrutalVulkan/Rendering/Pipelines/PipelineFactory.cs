using Brutal;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering.Pipelines;

public struct ShaderCreateInfo
{
    public String Name;
    public ShaderKind Kind;
}

public record struct PipelineBuildContext
{
    public VkFormat ColorFormat;
    public VkSampleCountFlags MsaaSamples;
}

public class PipelineFactory
{
    private readonly DeviceEx _device;
    private readonly PipelineManager _pipelineManager;
    private readonly DescriptorSetLayoutEx _descriptorSetLayout;
    private readonly PipelineBuildContext _context;

    public PipelineFactory(DeviceEx device, PipelineManager pipelineManager, PipelineBuildContext context,
        DescriptorSetLayoutEx descriptorSetLayout)
    {
        _device = device;
        _pipelineManager = pipelineManager;
        _descriptorSetLayout = descriptorSetLayout;
        _context = context;
    }

    public void CreateDefaultPipelines()
    {
        CreateOpaquePipeline();
        CreateWireframePipeline();
        CreateFlatColorPipeline();
        CreatePhongTexturePipeline();
    }

    private unsafe void CreateOpaquePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "mesh.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "mesh.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            },
            new()
            {
                Offset = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                Size = ByteSize.Of<PushConstantBlocks.FragmentBlock>(),
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

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
            RasterizationSamples = _context.MsaaSamples
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };

        var pipelineDescription = new PipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorSetLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddPipeline("Opaque", pipelineDescription);
    }

    private unsafe void CreateWireframePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "flatColor.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "flatColor.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            },
            new()
            {
                Offset = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                Size = ByteSize.Of<float4>(),
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

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
            RasterizationSamples = _context.MsaaSamples
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Line,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };

        var pipelineDescription = new PipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorSetLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddPipeline("Wireframe", pipelineDescription);
    }

    private unsafe void CreateFlatColorPipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "flatColor.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "flatColor.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            },
            new()
            {
                Offset = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                Size = ByteSize.Of<float4>(),
                StageFlags = VkShaderStageFlags.FragmentBit
            },
        ];

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
            RasterizationSamples = _context.MsaaSamples
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };

        var pipelineDescription = new PipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorSetLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddPipeline("Flat Color", pipelineDescription);
    }

    private unsafe void CreatePhongTexturePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "phongTexture.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "phongTexture.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            },
            new()
            {
                Offset = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                Size = ByteSize.Of<PushConstantBlocks.FragmentBlock>(),
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

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
            RasterizationSamples = _context.MsaaSamples
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };

        var pipelineDescription = new PipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorSetLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddPipeline("Phong Texture", pipelineDescription);
    }
}