using Brutal;
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
        CreateFlatTexturePipeline();
        CreateWireframePipeline();
        CreateFlatColorPipeline();
        CreatePhongTexturePipeline();
        CreatePhongColorPipeline();
        CreateSelectionMaskPipeline();
        CreateOutlineCompositePipeline();
        CreateSeedInitPipeline();
        CreateJumpFloodPipeline();
        CreateEntityIdPipeline();
        CreateAtmospherePipeline();
    }

    private unsafe void CreateFlatTexturePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "world_space.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "flat_texture.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.TextureFragmentBlock>(),
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

        _pipelineManager.AddGraphicsPipeline("Flat Texture", pipelineDescription);
    }

    private unsafe void CreateWireframePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "flat_color.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "flat_color.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.ColorFragmentBlock>(),
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
            new() { Name = "flat_color.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "flat_color.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.ColorFragmentBlock>(),
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

    private unsafe void CreatePhongColorPipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "world_space.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "phong_color.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.ColorFragmentBlock>(),
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

        _pipelineManager.AddGraphicsPipeline("Phong Color", pipelineDescription);
    }

    private unsafe void CreatePhongTexturePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "world_space.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "phong_texture.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.TextureFragmentBlock>(),
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
            new() { Name = "world_space.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "selection_mask.frag", Kind = ShaderKind.FragmentShader },
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
                Size = ByteSize.Of<PushConstantBlocks.TextureFragmentBlock>(),
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

    private unsafe void CreateOutlineCompositePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "fullscreen.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "outline_composite.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(0, 0);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format,
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
            RasterizationSamples = VkSampleCountFlags._1Bit
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
            DescriptorSetLayouts = [_descriptorManager.OutlineCompositeLayout],
            PushConstantRanges = [],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendColorAlpha.Value
        };

        _pipelineManager.AddGraphicsPipeline("Outline Composite", pipelineDescription);
    }

    private void CreateSeedInitPipeline()
    {
        var shader = new ShaderCreateInfo { Name = "seed_init.comp", Kind = ShaderKind.ComputeShader };
        var shaderStage = ShaderCreation.CreateShaderStage(shader, _device);

        var pipelineDescription = new ComputePipelineDescription
        {
            ShaderStage = shaderStage,
            DescriptorSetLayouts = [_descriptorManager.SeedLayout],
            PushConstantRanges = []
        };

        _pipelineManager.AddComputePipeline("Seed Init", pipelineDescription);

        _device.DestroyShaderModule(shaderStage.Module, null);
    }

    private void CreateJumpFloodPipeline()
    {
        var shader = new ShaderCreateInfo { Name = "jump_flood.comp", Kind = ShaderKind.ComputeShader };
        var shaderStage = ShaderCreation.CreateShaderStage(shader, _device);

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<UInt32>(),
                StageFlags = VkShaderStageFlags.ComputeBit
            }
        ];

        var pipelineDescription = new ComputePipelineDescription
        {
            ShaderStage = shaderStage,
            DescriptorSetLayouts = [_descriptorManager.SeedAToBLayout, _descriptorManager.SeedBToALayout],
            PushConstantRanges = [.. pushConstantRanges]
        };

        _pipelineManager.AddComputePipeline("Jump Flood", pipelineDescription);

        _device.DestroyShaderModule(shaderStage.Module, null);
    }

    private unsafe void CreateEntityIdPipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "world_space.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "entity_id.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

        var format = VkFormat.R32UInt;

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
                Size = ByteSize.Of<PushConstantBlocks.EntityIdBlock>(),
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

        _pipelineManager.AddGraphicsPipeline("Entity Id", pipelineDescription);
    }

    private unsafe void CreateAtmospherePipeline()
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "fullscreen.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "atmosphere.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(0, 0);

        var format = _context.ColorFormat;

        var rendering = new VkPipelineRenderingCreateInfo
        {
            ColorAttachmentCount = 1,
            ColorAttachmentFormats = &format
        };

        ReadOnlySpan<VkPushConstantRange> pushConstantRanges =
        [
            new()
            {
                Offset = ByteSize.Zero,
                Size = ByteSize.Of<PushConstantBlocks.AtmosphereBlock>(),
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

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
            RasterizationSamples = VkSampleCountFlags._1Bit
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
            DescriptorSetLayouts = [_descriptorManager.GlobalLayout, _descriptorManager.AtmosphereLayout],
            PushConstantRanges = [.. pushConstantRanges],
            ColorBlendStateCreateInfo = Presets.BlendState.BlendColorAlpha.Value
        };

        _pipelineManager.AddGraphicsPipeline("Atmosphere", pipelineDescription);
    }
}