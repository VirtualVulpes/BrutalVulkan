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
    private readonly DescriptorManager _descriptorManager;
    private readonly PipelineBuildContext _context;

    public PipelineFactory(DeviceEx device, PipelineManager pipelineManager, PipelineBuildContext context,
        DescriptorManager descriptorManager)
    {
        _device = device;
        _pipelineManager = pipelineManager;
        _descriptorManager = descriptorManager;
        _context = context;
    }

    public void CreateDefaultPipelines()
    {
        CreateOpaquePipeline();
        CreateWireframePipeline();
        CreateFlatColorPipeline();
        CreatePhongTexturePipeline();
        CreateTestComputePipeline();
        CreateSelectionMaskPipeline();
        CreateFullscreenTrianglePipeline();
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

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Opaque", pipelineDescription);
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

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Wireframe", pipelineDescription);
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

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Flat Color", pipelineDescription);
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

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Phong Texture", pipelineDescription);
    }

    private unsafe void CreateSelectionMaskPipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "mesh.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "selectionMask.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = VkFormat.R8UNorm;

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
            RasterizationSamples = VkSampleCountFlags._1Bit
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.BackBit,
            LineWidth = 1.0f
        };

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Selection Mask", pipelineDescription);
    }

    private unsafe void CreateFullscreenTrianglePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "fullscreen.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "display.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(0, 0);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
            DepthAttachmentFormat = VkFormat.D32SFloat
        };

        var depthStencil = new VkPipelineDepthStencilStateCreateInfo
        {
            DepthTestEnable = false,
            DepthWriteEnable = false,
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
            CullMode = VkCullModeFlags.None,
            LineWidth = 1.0f
        };

        var pipelineDescription = new GraphicsPipelineDescription
        {
            RenderingCreateInfo = rendering,
            ShaderStages = shaderStages,
            VertexInput = vertexInput,
            RasterizationStateCreateInfo = rasterizationState,
            MultisampleStateCreateInfo = multisampleState,
            DepthStencilStateCreateInfo = depthStencil,
            DescriptorSetLayouts = [_descriptorManager.DisplayLayout],
            PushConstantRanges = [],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendNone.Value
        };

        _pipelineManager.AddGraphicsPipeline("Fullscreen Triangle", pipelineDescription);
    }

    private void CreateTestComputePipeline()
    {
        var shader = new ShaderCreateInfo { Name = "test.comp", Kind = ShaderKind.ComputeShader };
        var shaderStage = ShaderCreation.CreateShaderStage(shader, _device);

        var pipelineDescription = new ComputePipelineDescription
        {
            ShaderStage = shaderStage,
            DescriptorSetLayouts = [_descriptorManager.ComputeLayout],
            PushConstantRanges = []
        };

        _pipelineManager.AddComputePipeline("Test Compute", pipelineDescription);

        _device.DestroyShaderModule(shaderStage.Module, null);
    }
}