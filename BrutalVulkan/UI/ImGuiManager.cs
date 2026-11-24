using Armature.Core;
using Armature.Input;
using Armature.Vulkan;
using Brutal;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.Utility;

namespace BrutalVulkan.UI;

public sealed class ImGuiManager : IDisposable, IFrameListener, IUiCaptureState
{
    private sealed class TextureBackend
    {
        public ImageEx Image;
        public ImageViewEx View;
        public VkDescriptorSet Set;
    }

    private VkSampler _fontSampler;
    private readonly BufferEx[] _vertexBuffers = new BufferEx[Constants.MaxFramesInFlight];
    private readonly BufferEx[] _indexBuffers = new BufferEx[Constants.MaxFramesInFlight];
    private readonly int[] _vertexCapacities = new int[Constants.MaxFramesInFlight];
    private readonly int[] _indexCapacities = new int[Constants.MaxFramesInFlight];
    private const int VertexHeadroom = 5000;
    private const int IndexHeadroom = 10000;

    private readonly Dictionary<int, TextureBackend> _textures = new();

    private readonly VmaAllocator _allocator;
    private readonly DeviceEx _device;
    private readonly CommandBufferManager _cmdManager;
    private readonly GlfwWindow _window;
    private readonly ImGuiContextPtr _context;
    private readonly InputManager _input;

    private DescriptorPoolEx _descriptorPool;

    public DescriptorSetLayoutEx DescriptorSetLayout { get; private set; }

    public Pipeline Pipeline { get; private set; }

    public BufferEx GetVertexBuffer(int frameIndex)
        => _vertexBuffers[frameIndex];

    public BufferEx GetIndexBuffer(int frameIndex)
        => _indexBuffers[frameIndex];

    public ImGuiManager(VmaAllocator allocator, DeviceEx device, CommandBufferManager cmdManager,
        GlfwWindow window, PipelineBuildContext context, InputManager input)
    {
        _allocator = allocator;
        _device = device;
        _cmdManager = cmdManager;
        _window = window;
        _input = input;

        _input.InputModeChanged += OnInputModeChanged;
        _input.Ui = this;

        _context = ImGui.CreateContext();
        if (_context.IsNull())
            throw new InvalidOperationException("Failed to create ImGui context.");

        var io = ImGui.GetIO();
        io.IniFilename = null; // stop ImGui.ini write
        UpdateDisplaySize();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        SetStyle();

        CreateResources();

        CreateImGuiPipeline(context);

        _window.OnKey += OnKey;
        _window.OnChar += OnChar;
        _window.OnMouseButton += OnMouseButton;
        _window.OnCursorPos += OnCursorPos;
    }

    private void SetStyle()
    {
        ImGui.StyleColorsDark();

        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ImGui.GetStyleColorVec4(ImGuiCol.TitleBg));

        ImGui.PushStyleColor(ImGuiCol.Tab, new float4(0.08f, 0.08f, 0.08f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabSelected, new float4(0.12f, 0.12f, 0.12f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new float4(0.16f, 0.16f, 0.16f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.FrameBg, new float4(0.035f, 0.035f, 0.035f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new float4(0.07f, 0.07f, 0.07f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new float4(0.10f, 0.10f, 0.10f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new float4(0.02f, 0.02f, 0.02f, 0.92f));

        ImGui.PushStyleColor(ImGuiCol.Header, new float4(0.06f, 0.06f, 0.06f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new float4(0.10f, 0.10f, 0.10f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new float4(0.13f, 0.13f, 0.13f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, new float4(0.12f, 0.12f, 0.12f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, new float4(0.16f, 0.16f, 0.16f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, new float4(0.20f, 0.20f, 0.20f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.Button, new float4(0.05f, 0.05f, 0.05f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new float4(0.08f, 0.08f, 0.08f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new float4(0.10f, 0.10f, 0.10f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.Separator, new float4(0.12f, 0.12f, 0.12f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorActive, new float4(0.16f, 0.16f, 0.16f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, new float4(0.20f, 0.20f, 0.20f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new float4(0.07f, 0.07f, 0.07f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new float4(0.12f, 0.12f, 0.12f, 1.0f));


        ImGui.PushStyleColor(ImGuiCol.MenuBarBg, new float4(0.12f, 0.12f, 0.12f, 1.0f));

        var style = ImGui.GetStyle();
        style.FontSizeBase = 16.0f;
        style.WindowBorderSize = 0.0f;
        style.FrameRounding = 4.0f;
        style.WindowRounding = 4.0f;
    }

    private void CreateResources()
    {
        var binding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit
        };

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = [binding]
        };

        DescriptorSetLayout = _device.CreateDescriptorSetLayout(layoutInfo, null);

        var poolSize = new VkDescriptorPoolSize
        {
            Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = 32
        };

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 32,
            PoolSizes = [poolSize]
        };

        _descriptorPool = _device.CreateDescriptorPool(poolInfo, null);

        var samplerInfo = new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.ClampToEdge,
            AddressModeV = VkSamplerAddressMode.ClampToEdge,
            AddressModeW = VkSamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            MaxAnisotropy = 1.0f,
            CompareEnable = false,
            CompareOp = VkCompareOp.Always,
            BorderColor = VkBorderColor.FloatOpaqueWhite,
            UnnormalizedCoordinates = false,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f
        };
        _fontSampler = _device.CreateSampler(samplerInfo, null);
    }

    private unsafe void CreateImGuiPipeline(PipelineBuildContext context)
    {
        var shaders = new ShaderCreateInfo[]
        {
            new() { Name = "imgui.vert", Kind = ShaderKind.VertexShader },
            new() { Name = "imgui.frag", Kind = ShaderKind.FragmentShader },
        };
        using var shaderStages = ShaderCreation.CreateShaderStages(shaders, _device);

        var vertexInput = new VertexInput(1, 3).AddBinding(0, ByteSize.Of<ImDrawVert>(), VkVertexInputRate.Vertex)
            .AddAttribute(0, 0, VkFormat.R32G32SFloat, ByteSize.Zero)
            .AddAttribute(1, 0, VkFormat.R32G32SFloat, ByteSize.Of(8))
            .AddAttribute(2, 0, VkFormat.R8G8B8A8UNorm, ByteSize.Of(16));

        var format = context.ColorFormat;

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
                Size = ByteSize.Of<PushConstantBlocks.VertexBlock>(),
                StageFlags = VkShaderStageFlags.VertexBit
            }
        ];

        var depthStencil = new VkPipelineDepthStencilStateCreateInfo
        {
            DepthTestEnable = false,
            DepthWriteEnable = false,
            DepthCompareOp = VkCompareOp.LessOrEqual,
            DepthBoundsTestEnable = false,
            StencilTestEnable = false
        };

        var multisampleState = new VkPipelineMultisampleStateCreateInfo
        {
            SampleShadingEnable = false,
            RasterizationSamples = VkSampleCountFlags._1Bit
        };

        var rasterizationState = new VkPipelineRasterizationStateCreateInfo
        {
            PolygonMode = VkPolygonMode.Fill,
            CullMode = VkCullModeFlags.None,
            LineWidth = 1.0f,
        };

        var colorBlendState = new VkPipelineColorBlendAttachmentState
        {
            ColorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit |
                             VkColorComponentFlags.ABit,
            BlendEnable = true,
            SrcColorBlendFactor = VkBlendFactor.SrcAlpha,
            DstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = VkBlendOp.Add,
            SrcAlphaBlendFactor = VkBlendFactor.One,
            DstAlphaBlendFactor = VkBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = VkBlendOp.Add
        };

        var colorBlendStateCreateInfo = new VkPipelineColorBlendStateCreateInfo
        {
            LogicOpEnable = false,
            AttachmentCount = 1,
            Attachments = &colorBlendState
        };

        var viewportState = new VkPipelineViewportStateCreateInfo
        {
            ViewportCount = 1,
            ScissorCount = 1,
        };

        Span<VkDynamicState> dynamicStates = [VkDynamicState.Viewport, VkDynamicState.Scissor];

        var pipelineLayout = _device.CreatePipelineLayout([DescriptorSetLayout],
            pushConstantRanges, null);

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
                    Next = &rendering,
                    StageCount = shaderStages.Count,
                    Stages = shaderStages,
                    VertexInputState = vertexInput,
                    InputAssemblyState = Presets.InputAssembly.TriangleList,
                    ViewportState = &viewportState,
                    RasterizationState = &rasterizationState,
                    MultisampleState = &multisampleState,
                    ColorBlendState = &colorBlendStateCreateInfo,
                    Layout = pipelineLayout,
                    DynamicState = &dynamicState,
                    DepthStencilState = &depthStencil
                },
                null
            );

            Pipeline = new Pipeline(_device, pipeline, pipelineLayout, new PipelineId(0));
        }
    }


    public void NewFrame(float deltaTime)
    {
        ImGui.GetIO().DeltaTime = float.Max(deltaTime, 1e-5f);
        ImGui.NewFrame();

        ImString.SharedStorage.Reset();
    }

    public void RenderFrame(int frameIndex)
    {
        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid) return;

        if (drawData.Textures.IsNotNull())
        {
            foreach (var texture in drawData.Textures.Value)
            {
                if (texture.Status != ImTextureStatus.OK)
                    UpdateTexture(texture);
            }
        }

        UpdateBuffers(frameIndex);
    }

    private void UpdateTexture(ImTextureDataPtr texture)
    {
        switch (texture.Status)
        {
            case ImTextureStatus.WantCreate:
                if (texture.Pixels.IsNull()) return;
                CreateTexture(texture);
                UploadTexture(texture, VkImageLayout.Undefined);
                break;
            case ImTextureStatus.WantUpdates:
                if (_textures.ContainsKey(texture.UniqueID))
                    UploadTexture(texture, VkImageLayout.ShaderReadOnlyOptimal);
                break;
            case ImTextureStatus.WantDestroy:
                if (texture.UnusedFrames >= Constants.MaxFramesInFlight)
                    DestroyTexture(texture);
                break;
        }
    }

    private unsafe void CreateTexture(ImTextureDataPtr texture)
    {
        var format = (texture.BytesPerPixel == 4) ? VkFormat.R8G8B8A8UNorm : VkFormat.R8UNorm;

        var createInfo = new ImageEx.CreateInfo
        {
            Name = "ImGui Texture " + texture.UniqueID,
            ImageType = VkImageType._2D,
            ImageFormat = format,
            ImageExtent = new VkExtent3D
            {
                Width = texture.Width,
                Height = texture.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };
        var image = _allocator.CreateImage(createInfo);

        var range = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LayerCount = 1,
            LevelCount = 1
        };
        var view = image.CreateImageView(VkImageViewType._2D, range, image.Allocator.VkAllocator);

        var set = _device.AllocateDescriptorSet(_descriptorPool, DescriptorSetLayout);

        var imageInfo = new VkDescriptorImageInfo
        {
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
            ImageView = view.VkImageView,
            Sampler = _fontSampler
        };
        var write = new VkWriteDescriptorSet
        {
            DstSet = set,
            DescriptorCount = 1,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            ImageInfo = &imageInfo,
            DstBinding = 0
        };
        _device.UpdateDescriptorSets([write], []);

        _textures[texture.UniqueID] = new TextureBackend
        {
            Image = image,
            View = view,
            Set = set
        };
        texture.SetTexID((ImTextureID)set.VkHandle);
    }

    private unsafe void UploadTexture(ImTextureDataPtr texture, VkImageLayout oldLayout)
    {
        var backend = _textures[texture.UniqueID];
        var uploadSize = (long)texture.Width * texture.Height * texture.BytesPerPixel;

        var bufferInfo = new BufferEx.CreateInfo
        {
            Name = "ImGui Staging Buffer",
            BufferSize = ByteSize.Of(uploadSize),
            BufferUsage = VkBufferUsageFlags.TransferSrcBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };
        var stagingBuffer = _allocator.CreateBuffer(bufferInfo);

        var memory = stagingBuffer.Map();
        Buffer.MemoryCopy(texture.Pixels, memory.MapPtr.Pointer, uploadSize, uploadSize);
        memory.Unmap();

        var cmd = _cmdManager.BeginSingleTimeCommands();

        ImageUtility.TransitionImage(backend.Image.VkImage, oldLayout, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TransferBit, VkAccessFlags.None,
            VkAccessFlags.TransferWriteBit, VkImageAspectFlags.ColorBit, cmd);
        ImageUtility.CopyBufferToImage(cmd, stagingBuffer.VkBuffer, backend.Image, texture.Width, texture.Height);
        ImageUtility.TransitionImage(backend.Image.VkImage, VkImageLayout.TransferDstOptimal,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.TransferWriteBit,
            VkAccessFlags.ShaderReadBit, VkImageAspectFlags.ColorBit, cmd);

        _cmdManager.EndSingleTimeCommands(cmd);
        stagingBuffer.Dispose();

        texture.SetStatus(ImTextureStatus.OK);
    }

    private void DestroyTexture(ImTextureDataPtr texture)
    {
        if (_textures.Remove(texture.UniqueID, out var backend))
            FreeBackend(backend);

        texture.SetTexID(ImTextureID.Invalid);
        texture.SetStatus(ImTextureStatus.Destroyed);
    }

    private void FreeBackend(TextureBackend backend)
    {
        _device.FreeDescriptorSet(_descriptorPool, backend.Set);
        backend.View.Dispose();
        backend.Image.Dispose();
    }

    private unsafe void UpdateBuffers(int frameIndex)
    {
        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0) return;

        if (drawData.TotalVtxCount > _vertexCapacities[frameIndex])
        {
            if (_vertexBuffers[frameIndex].IsNotNull())
                _vertexBuffers[frameIndex].Dispose();
            _vertexBuffers[frameIndex] = default;

            var newCapacity = drawData.TotalVtxCount + VertexHeadroom;

            var vertexCreateInfo = new BufferEx.CreateInfo
            {
                Name = "ImGui Vertex Buffer",
                BufferFlags = VkBufferCreateFlags.None,
                BufferSize = ByteSize.Of(newCapacity * sizeof(ImDrawVert)),
                BufferUsage = VkBufferUsageFlags.VertexBufferBit,
                BufferSharingMode = VkSharingMode.Exclusive,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };

            _vertexBuffers[frameIndex] = _allocator.CreateBuffer(vertexCreateInfo);
            _vertexCapacities[frameIndex] = newCapacity;
        }

        if (drawData.TotalIdxCount > _indexCapacities[frameIndex])
        {
            if (_indexBuffers[frameIndex].IsNotNull())
                _indexBuffers[frameIndex].Dispose();
            _indexBuffers[frameIndex] = default;

            var newCapacity = drawData.TotalIdxCount + IndexHeadroom;

            var indexCreateInfo = new BufferEx.CreateInfo
            {
                Name = "ImGui Index Buffer",
                BufferFlags = VkBufferCreateFlags.None,
                BufferSize = ByteSize.Of(newCapacity * sizeof(ushort)),
                BufferUsage = VkBufferUsageFlags.IndexBufferBit,
                BufferSharingMode = VkSharingMode.Exclusive,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };

            _indexBuffers[frameIndex] = _allocator.CreateBuffer(indexCreateInfo);
            _indexCapacities[frameIndex] = newCapacity;
        }

        if (drawData is not { TotalVtxCount: > 0, TotalIdxCount: > 0 }) return;

        var vtxMemory = _vertexBuffers[frameIndex].Map();
        var idxMemory = _indexBuffers[frameIndex].Map();

        var vtxDst = (ImDrawVert*)vtxMemory.MapPtr;
        var idxDst = (ushort*)idxMemory.MapPtr;

        foreach (var cmdList in drawData.CmdLists)
        {
            Buffer.MemoryCopy(cmdList.VtxBuffer.Data, vtxDst, cmdList.VtxBuffer.Count * sizeof(ImDrawVert),
                cmdList.VtxBuffer.Count * sizeof(ImDrawVert));
            Buffer.MemoryCopy(cmdList.IdxBuffer.Data, idxDst, cmdList.IdxBuffer.Count * sizeof(ushort),
                cmdList.IdxBuffer.Count * sizeof(ushort));
            vtxDst += cmdList.VtxBuffer.Count;
            idxDst += cmdList.IdxBuffer.Count;
        }

        vtxMemory.Unmap();
        idxMemory.Unmap();
    }

    public void UpdateDisplaySize()
    {
        if (_window.Size is not { X: > 0, Y: > 0 }) return;

        var framebufferSize = _window.FramebufferSize;

        var io = ImGui.GetIO();
        io.DisplaySize = new float2(framebufferSize.X, framebufferSize.Y);
    }

    private void OnMouseButton(GlfwWindow window, GlfwMouseButton button, GlfwButtonAction action, GlfwModifier mods)
    {
        ImGui.GetIO().AddMouseButtonEvent((int)button, action == GlfwButtonAction.Press);
    }

    private void OnKey(GlfwWindow window, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mods)
    {
        var io = ImGui.GetIO();

        io.AddKeyEvent(ImGuiKey.Mod_Ctrl, mods.HasFlag(GlfwModifier.Control));
        io.AddKeyEvent(ImGuiKey.Mod_Shift, mods.HasFlag(GlfwModifier.Shift));
        io.AddKeyEvent(ImGuiKey.Mod_Alt, mods.HasFlag(GlfwModifier.Alt));
        io.AddKeyEvent(ImGuiKey.Mod_Super, mods.HasFlag(GlfwModifier.Super));

        var imKey = GlfwImGuiTranslation.ToImGuiKey(key);
        if (imKey == ImGuiKey.None) return;

        var pressed = action is GlfwKeyAction.Press or GlfwKeyAction.Repeat;

        if (pressed)
        {
            if (_input.InputMode != InputMode.UiInteraction) return;

            if (imKey == ImGuiKey.Tab && !io.WantTextInput) return;
        }

        ImGui.GetIO().AddKeyEvent(imKey, pressed);
    }

    private void OnChar(GlfwWindow window, uint codepoint)
    {
        if (_input.InputMode != InputMode.UiInteraction) return;
        ImGui.GetIO().AddInputCharacter(codepoint);
    }

    private void OnCursorPos(GlfwWindow window, double2 pos)
    {
        var scale = window.ContentScale;
        ImGui.GetIO().AddMousePosEvent((float)pos.X * scale.X, (float)pos.Y * scale.Y);
    }

    public void Dispose()
    {
        foreach (var backend in _textures.Values)
        {
            FreeBackend(backend);
        }

        _textures.Clear();

        if (_fontSampler.IsNotNull())
            _device.DestroySampler(_fontSampler, null);

        for (var i = 0; i < _vertexBuffers.Length; i++)
        {
            if (_vertexBuffers[i].IsNotNull())
                _vertexBuffers[i].Dispose();
            if (_indexBuffers[i].IsNotNull())
                _indexBuffers[i].Dispose();
        }

        _device.DestroyDescriptorSetLayout(DescriptorSetLayout, null);
        _device.DestroyDescriptorPool(_descriptorPool, null);

        Pipeline.Dispose();

        _input.InputModeChanged -= OnInputModeChanged;
        if (_input.Ui == this) _input.Ui = null;
        
        _window.OnKey -= OnKey;
        _window.OnChar -= OnChar;
        _window.OnMouseButton -= OnMouseButton;
        _window.OnCursorPos -= OnCursorPos;

        ImGui.DestroyContext(_context);
    }

    public void OnResize(RenderTargets targets)
    {
        UpdateDisplaySize();
    }

    public void OnPrepare(RenderContext ctx)
    {
        RenderFrame(ctx.FrameIndex);
    }

    private void OnInputModeChanged(InputMode mode, double2 pos)
    {
        var io = ImGui.GetIO();
        if (mode == InputMode.CameraControl)
        {
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse | ImGuiConfigFlags.NoMouseCursorChange;
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        }
        else
        {
            io.ConfigFlags &= ~(ImGuiConfigFlags.NoMouse | ImGuiConfigFlags.NoMouseCursorChange);
            io.AddMousePosEvent((float)pos.X, (float)pos.Y);
        }
    }

    public bool WantsKeyboard => ImGui.GetIO().WantCaptureKeyboard;
    public bool WantsMouse => ImGui.GetIO().WantCaptureMouse;
}