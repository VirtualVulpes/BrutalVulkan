using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.Materials;
using BrutalVulkan.Rendering;

namespace BrutalVulkan;

public record struct Vertex(float3 Position, float3 Normal, float2 Uv);

public sealed class App : IDisposable
{
    private readonly GlfwWindow _window;
    private GCHandle _gcHandle;

    private readonly InputManager _inputManager;
    private readonly Camera _camera;

    private Instance _instance;
    private PhysicalDevice? _physicalDevice;
    private DeviceEx _device;
    private int _queueFamily = -1;
    private Queue _queue;
    private VkSurfaceKHR _surface;
    private VmaAllocator _allocator;

    private VkSampler _textureSampler;

    private VkDescriptorSetLayout _descriptorSetLayout;
    private VkDescriptorPool _descriptorPool;
    private VkDescriptorSet[] _descriptorSets;

    private VkCommandPool _commandPool;

    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;
    private ModelLoader _modelLoader;
    private MeshManager _meshManager;
    private MaterialManager _materialManager;
    private Renderer _renderer;

    private ImGuiManager _imGuiManager;

    private CommandBufferManager _cmdManager;

    private readonly List<RenderObject> _renderObjects = [];

    public App()
    {
        InitGlfw();

        _window = Glfw.CreateWindow(
            new GlfwWindow.CreateInfo
                { Title = "Brutal Vulkan", Size = new int2(Constants.WindowHeight, Constants.WindowHeight) }
        );
        Glfw.PInvoke.SetInputMode(_window, (Int32)GlfwInputMode.Cursor, (Int32)GlfwCursorMode.Disabled);
        _gcHandle = GCHandle.Alloc(this);
        unsafe
        {
            _window.UserPointer = GCHandle.ToIntPtr(_gcHandle).ToPointer();
        }

        _window.OnFramebufferSize += OnFramebufferSize;

        _inputManager = new InputManager(_window);

        _camera = new Camera();

        InitVulkan();
    }

    private void InitGlfw()
    {
        if (!Glfw.Init())
        {
            var errorCode = Glfw.GetError(out var errorMessage);
            throw new InvalidOperationException($"Failed to initialize GLFW: {errorCode}, {errorMessage}");
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_NO_API);
        Glfw.WindowHint(GlfwWindowHint.Resizable, Glfw.Constants.GLFW_TRUE);
    }

    private static unsafe void OnFramebufferSize(GlfwWindow window, int2 size)
    {
        var handle = GCHandle.FromIntPtr((nint)window.UserPointer);
        var app = (App)handle.Target!;

        app._renderer.OnFramebufferResize();
        app._imGuiManager.OnFramebufferResize(size);
        app._camera.AspectRatio = (Single)size.X / size.Y;
    }

    private unsafe void InitVulkan()
    {
        _ = Vulkan.GetInstanceProcAddr;

        var instanceExtensions = new HashSet<String>();
        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions);
        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        for (var i = 0; i < glfwExtensionCount; i++)
            instanceExtensions.Add(Utf8.GetString(glfwExtensions[i]));

        var layers = new HashSet<String>();
        VulkanHelpers.AddValidationLayers(layers);

        _instance = Vulkan.CreateInstance(
            new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = (Int32)VulkanHelpers.Api.VERSION_1_3 },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = instanceExtensions,
                EnabledLayers = layers,
            }
        );

        var result = Glfw.CreateWindowSurface(_instance.VkHandle, _window, out var surfaceHandle);

        if (result != (Int32)VkResult.Success)
            throw new InvalidOperationException($"Failed to create Vulkan surface: {result}");

        _surface = new VkSurfaceKHR((nint)surfaceHandle);

        foreach (var candidate in _instance.PhysicalDevices)
        {
            var families = new VkQueueFamilyProperties[candidate.GetQueueFamilyProperties_Count()];
            candidate.GetQueueFamilyProperties_Write(families);

            for (var i = 0; i < families.Length && _physicalDevice is null; i++)
            {
                if ((families[i].QueueFlags & VkQueueFlags.GraphicsBit) != 0 &&
                    candidate.GetSurfaceSupportKHR(i, _surface))
                    (_physicalDevice, _queueFamily) = (candidate, i);
            }
        }

        if (_physicalDevice is null)
            throw new InvalidOperationException(
                "No Vulkan device has a queue family that can draw and present to the window");

        var deviceExtensions = new HashSet<String>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, _physicalDevice);

        var priority = 1.0f;

        var vulkan12Features = new VkPhysicalDeviceVulkan12Features
            { ShaderSampledImageArrayNonUniformIndexing = true, DescriptorBindingPartiallyBound = true };
        var vulkan13Features = new VkPhysicalDeviceVulkan13Features
            { DynamicRendering = true, Next = &vulkan12Features };
        var physicalFeatures = new VkPhysicalDeviceFeatures2()
            { Features = { SamplerAnisotropy = true, FillModeNonSolid = true }, Next = &vulkan13Features };

        _device = _physicalDevice.CreateDevice(
            new DeviceEx.CreateInfo
            {
                Extensions = deviceExtensions,
                QueueCreateInfos =
                [
                    new VkDeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = _queueFamily,
                        QueueCount = 1,
                        QueuePriorities = &priority,
                    }
                ],
                Next = &physicalFeatures
            }
        );

        _queue = _device.GetQueue(_queueFamily, 0);

        _pipelineManager = new PipelineManager(_device);

        CreateCommandPool();

        _cmdManager = new CommandBufferManager(_device, _queue, _commandPool);

        _allocator = _device.CreateVmaAllocator(
            new VmaAllocatorCreateInfo
            {
                Instance = _instance,
                PhysicalDevice = _physicalDevice,
                Device = _device,
                VulkanApiVersion = (UInt32)VulkanHelpers.Api.VERSION_1_3
            }
        );

        CreateDescriptorSets();

        CreateTextureSampler();

        _imGuiManager = new ImGuiManager(_allocator, _device,
            new float2(_window.Width, _window.Height), _cmdManager, _window, _inputManager);

        _renderer = new Renderer(_device, _physicalDevice, _surface, _window, _allocator, _commandPool, _queue,
            _pipelineManager, _descriptorSets, _imGuiManager);

        CreatePipelines();

        CreateMeshes();
        LoadTextures();
        LoadModels();
        CreateMaterials();
        CreateObjects();
    }

    public void Run()
    {
        var last = Stopwatch.GetTimestamp();

        while (!_window.ShouldClose)
        {
            var now = Stopwatch.GetTimestamp();
            var dt = Single.Min((Single)Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.1f);
            last = now;

            _inputManager.Update();

            Glfw.PollEvents();

            if (_inputManager.IsKeyPressed(GlfwKey.Escape))
                _window.ShouldClose = true;

            switch (_inputManager.InputMode)
            {
                case InputMode.CameraControl when _inputManager.IsKeyPressed(GlfwKey.Tab):
                    _inputManager.SetInputMode(InputMode.UiInteraction);
                    break;
                case InputMode.CameraControl:
                {
                    var movement = float3.Zero;
                    var speed = 5.0f;
                    if (_inputManager.IsKeyDown(GlfwKey.W))
                        movement += float3.UnitZ;
                    if (_inputManager.IsKeyDown(GlfwKey.S))
                        movement -= float3.UnitZ;
                    if (_inputManager.IsKeyDown(GlfwKey.A))
                        movement -= float3.UnitX;
                    if (_inputManager.IsKeyDown(GlfwKey.D))
                        movement += float3.UnitX;
                    if (_inputManager.IsKeyDown(GlfwKey.Space))
                        movement += float3.UnitY;
                    if (_inputManager.IsKeyDown(GlfwKey.LeftControl))
                        movement -= float3.UnitY;
                    _camera.MoveRelative(float3.NormalizeOrZero(movement) * dt * speed);

                    var mouseDelta = _inputManager.MouseDelta;
                    var sensitivity = 0.001f;
                    _camera.Rotate((Single)mouseDelta.X * sensitivity, -(Single)mouseDelta.Y * sensitivity);
                    break;
                }
                case InputMode.UiInteraction:
                {
                    if (_inputManager.IsMouseButtonPressed(GlfwMouseButton.Left) &&
                        !InputManager.IsImGuiCapturingMouse())
                    {
                        _inputManager.SetInputMode(InputMode.CameraControl);
                    }

                    break;
                }
            }

            _imGuiManager.NewFrame();

            CreateUI();

            _renderer.DrawFrame([.. _renderObjects], _camera.GetView() * _camera.GetProjection());
        }
    }

    private void CreateUI()
    {
        ImGui.Begin("Hello ImGui"u8);
        ImGui.Text("Hello, world!"u8);
        ImGui.End();
    }

    private void CreateCommandPool()
    {
        _commandPool = _device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = _queueFamily,
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit
            },
            null
        );
    }

    private unsafe void CreatePipelines()
    {
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();

        VkShaderModule CompileShader(ReadOnlySpan<Byte> glsl, ShaderKind kind)
        {
            using var spirv = compiler.Compile(glsl, kind, "shader"u8, "main"u8, options);
            return ShaderModuleExtensions.FromSpirv(
                _device,
                new ReadOnlySpan<Byte>(spirv.GetBytes(), spirv.GetLength()),
                null
            );
        }

        // Pipeline 1
        {
            using var shaderStages = new ShaderStages(
                _device,
                [
                    Presets.ShaderStage.Vertex(
                        CompileShader(
                            LoadShader("mesh.vert"),
                            ShaderKind.VertexShader
                        )
                    ),
                    Presets.ShaderStage.Fragment(
                        CompileShader(
                            LoadShader("mesh.frag"),
                            ShaderKind.FragmentShader
                        )
                    )
                ],
                null
            );

            var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

            var format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<float4x4>(),
                    StageFlags = VkShaderStageFlags.VertexBit
                },
                new()
                {
                    Offset = ByteSize.Of<float4x4>(),
                    Size = ByteSize.Of<UInt32>(),
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
                RasterizationSamples = _renderer.MsaaSamples
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

        // Wireframe
        {
            using var shaderStages = new ShaderStages(
                _device,
                [
                    Presets.ShaderStage.Vertex(
                        CompileShader(
                            LoadShader("flatColor.vert"),
                            ShaderKind.VertexShader
                        )
                    ),
                    Presets.ShaderStage.Fragment(
                        CompileShader(
                            LoadShader("flatColor.frag"),
                            ShaderKind.FragmentShader
                        )
                    )
                ],
                null
            );

            var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

            var format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<float4x4>(),
                    StageFlags = VkShaderStageFlags.VertexBit
                },
                new()
                {
                    Offset = ByteSize.Of<float4x4>(),
                    Size = ByteSize.Of<float3>(),
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
                RasterizationSamples = _renderer.MsaaSamples
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

        // ImGui
        {
            using var shaderStages = new ShaderStages(
                _device,
                [
                    Presets.ShaderStage.Vertex(
                        CompileShader(
                            LoadShader("imgui.vert"),
                            ShaderKind.VertexShader
                        )
                    ),
                    Presets.ShaderStage.Fragment(
                        CompileShader(
                            LoadShader("imgui.frag"),
                            ShaderKind.FragmentShader
                        )
                    )
                ],
                null
            );

            var vertexInput = new VertexInput(1, 3).AddBinding(0, ByteSize.Of<ImDrawVert>(), VkVertexInputRate.Vertex)
                .AddAttribute(0, 0, VkFormat.R32G32SFloat, ByteSize.Zero)
                .AddAttribute(1, 0, VkFormat.R32G32SFloat, ByteSize.Of(8))
                .AddAttribute(2, 0, VkFormat.R8G8B8A8UNorm, ByteSize.Of(16));

            var format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<PushConstantBlocks.ImGuiBlock>(),
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
                RasterizationSamples = _renderer.MsaaSamples
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

            var pipelineDescription = new PipelineDescription
            {
                RenderingCreateInfo = rendering,
                ShaderStages = shaderStages,
                VertexInput = vertexInput,
                RasterizationStateCreateInfo = rasterizationState,
                MultisampleStateCreateInfo = multisampleState,
                DepthStencilStateCreateInfo = depthStencil,
                DescriptorSetLayouts = [_imGuiManager.DescriptorSetLayout],
                PushConstantRanges = [.. pushConstantRanges],
                ColorBlendStateCreateInfo = colorBlendStateCreateInfo
            };

            _pipelineManager.AddPipeline("ImGui", pipelineDescription);
        }

        // Flat Color
        {
            using var shaderStages = new ShaderStages(
                _device,
                [
                    Presets.ShaderStage.Vertex(
                        CompileShader(
                            LoadShader("flatColor.vert"),
                            ShaderKind.VertexShader
                        )
                    ),
                    Presets.ShaderStage.Fragment(
                        CompileShader(
                            LoadShader("flatColor.frag"),
                            ShaderKind.FragmentShader
                        )
                    )
                ],
                null
            );

            var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

            var format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<float4x4>(),
                    StageFlags = VkShaderStageFlags.VertexBit
                },
                new()
                {
                    Offset = ByteSize.Of<float4x4>(),
                    Size = ByteSize.Of<float3>(),
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
                RasterizationSamples = _renderer.MsaaSamples
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
    }

    private void CreateMeshes()
    {
        _meshManager = new MeshManager(_allocator);

        // plane
        {
            _meshManager.AddMesh("plane", new Mesh(
                [
                    new Vertex(new float3(-0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 0.0f))
                ],
                [
                    0, 1, 2, 2, 3, 0
                ]
            ));
        }

        // cube
        {
            _meshManager.AddMesh("cube", new Mesh(
                [
                    new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 0.0f)),

                    new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(0.0f, 0.0f)),

                    new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(0.0f, 0.0f)),

                    new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(0.0f, 0.0f)),

                    new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(0.0f, 0.0f)),

                    new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(0.0f, 0.0f))
                ],
                [
                    0, 1, 2, 2, 3, 0,
                    4, 5, 6, 6, 7, 4,
                    8, 9, 10, 10, 11, 8,
                    12, 13, 14, 14, 15, 12,
                    16, 17, 18, 18, 19, 16,
                    20, 21, 22, 22, 23, 20
                ]
            ));
        }

        // planet
        {
            CubeSphere.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _meshManager.AddMesh("planet", CubeSphere.CreateMesh(3, 2.0f));
        }
    }

    private void LoadTextures()
    {
        _textureManager = new TextureManager(_physicalDevice.GetFeatures().TextureCompressionBC);

        var commandBuffer = _cmdManager.BeginSingleTimeCommands();

        foreach (var fileName in new[]
                 {
                     "brick.ktx2",
                     "statue.ktx2",
                     "viking_room.ktx2"
                 })
        {
            var textureId = _textureManager.AddTexture(fileName, commandBuffer, _allocator, TextureColorSpace.SRgb);
            UpdateTextureDescriptor(textureId);
        }

        _cmdManager.EndSingleTimeCommands(ref commandBuffer);
        _textureManager.ClearStagingBuffers();
    }

    private void LoadModels()
    {
        _modelLoader = new ModelLoader();
        var mesh = _modelLoader.LoadModel("viking_room.gltf");
        _meshManager.AddMesh("viking_room", mesh);
    }

    private void CreateMaterials()
    {
        _materialManager = new MaterialManager();

        _materialManager.AddMaterial("brick",
            new TextureMaterial
            {
                PipelineId = _pipelineManager.GetPipelineId("Opaque"),
                TextureId = _textureManager.GetTextureId("brick")
            });
        _materialManager.AddMaterial("statue",
            new TextureMaterial
            {
                PipelineId = _pipelineManager.GetPipelineId("Opaque"),
                TextureId = _textureManager.GetTextureId("statue")
            });
        _materialManager.AddMaterial("viking_room",
            new TextureMaterial
            {
                PipelineId = _pipelineManager.GetPipelineId("Opaque"),
                TextureId = _textureManager.GetTextureId("viking_room")
            });
        _materialManager.AddMaterial("wireframe",
            new ColorMaterial
            {
                PipelineId = _pipelineManager.GetPipelineId("Wireframe"),
                Color = new float3(0.3f, 0.1f, 0.3f)
            });
        _materialManager.AddMaterial("flat color",
            new ColorMaterial
            {
                PipelineId = _pipelineManager.GetPipelineId("Flat Color"),
                Color = new float3(0.7f, 0.5f, 0.5f)
            });
    }

    private void CreateObjects()
    {
        AddObject(
            _meshManager.GetMeshId("plane"),
            new Transform
            {
                Position = new float3(0.0f, -2.5f, 0.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-90.0f)),
                Scale = new float3(10.0f, 10.0f, 1.0f)
            },
            _materialManager.GetMaterialId("brick")
        );

        AddObject(
            _meshManager.GetMeshId("cube"),
            new Transform
            {
                Position = new float3(0.0f, 5.0f, -1.0f),
                Rotation = floatQuat.Identity,
                Scale = new float3(1.0f, 1.0f, 1.0f)
            },
            _materialManager.GetMaterialId("statue")
        );

        AddObject(
            _meshManager.GetMeshId("viking_room"),
            new Transform
            {
                Position = new float3(0.0f, 1.0f, 0.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-90f)),
                Scale = new float3(1.0f, 1.0f, 1.0f)
            },
            _materialManager.GetMaterialId("viking_room")
        );

        AddObject(
            _meshManager.GetMeshId("planet"),
            new Transform
            {
                Position = new float3(3.0f, 1.0f, -2.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-90f)),
                Scale = new float3(1.0f, 1.0f, 1.0f)
            },
            _materialManager.GetMaterialId("flat color")
        );

        AddObject(
            _meshManager.GetMeshId("planet"),
            new Transform
            {
                Position = new float3(-2.0f, 1.0f, -2.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-90f)),
                Scale = new float3(1.0f, 1.0f, 1.0f)
            },
            _materialManager.GetMaterialId("wireframe")
        );
    }

    private void AddObject(MeshId mesh, Transform transform, MaterialId materialId)
    {
        _renderObjects.Add(new RenderObject
        {
            Mesh = _meshManager.GetGpuMesh(mesh),
            ModelMatrix = transform.GetModelMatrix(),
            Material = _materialManager.GetMaterial(materialId)
        });
    }

    public static void TransitionImage(
        VkImage image,
        VkImageLayout from,
        VkImageLayout to,
        VkPipelineStageFlags srcStage,
        VkPipelineStageFlags dstStage,
        VkAccessFlags srcAccess,
        VkAccessFlags dstAccess,
        VkImageAspectFlags imageAspectFlags,
        CommandBuffer commandBuffer,
        Int32 mipLevels = 1,
        Int32 baseMipLevel = 0
    )
    {
        commandBuffer.PipelineBarrier(
            srcStage,
            dstStage,
            VkDependencyFlags.None,
            [],
            [],
            [
                new VkImageMemoryBarrier
                {
                    SrcAccessMask = srcAccess,
                    DstAccessMask = dstAccess,
                    OldLayout = from,
                    NewLayout = to,
                    SrcQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    DstQueueFamilyIndex = VK.QUEUE_FAMILY_IGNORED,
                    Image = image,
                    SubresourceRange = new VkImageSubresourceRange
                    {
                        AspectMask = imageAspectFlags,
                        LayerCount = 1,
                        LevelCount = mipLevels,
                        BaseArrayLayer = 0,
                        BaseMipLevel = baseMipLevel
                    }
                }
            ]
        );
    }

    private unsafe void CreateDescriptorSets()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = Constants.MaxTextures,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        VkDescriptorBindingFlags[] flags =
        [
            VkDescriptorBindingFlags.PartiallyBoundBit
        ];

        fixed (VkDescriptorSetLayoutBinding* bindingsPtr = bindings)
        {
            fixed (VkDescriptorBindingFlags* flagsPtr = flags)
            {
                var bindingFlagsInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo
                {
                    BindingCount = 1,
                    BindingFlags = flagsPtr
                };

                var layoutInfo = new VkDescriptorSetLayoutCreateInfo
                {
                    Next = &bindingFlagsInfo,
                    BindingCount = bindings.Length,
                    Bindings = bindingsPtr
                };

                _descriptorSetLayout = _device.CreateDescriptorSetLayout(layoutInfo, null);
            }
        }

        VkDescriptorPoolSize[] pools =
        [
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = Constants.MaxTextures
            }
        ];

        fixed (VkDescriptorPoolSize* poolSizes = pools)
        {
            var poolInfo = new VkDescriptorPoolCreateInfo
            {
                Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = 1,
                PoolSizeCount = pools.Length,
                PoolSizes = poolSizes
            };

            _descriptorPool = _device.CreateDescriptorPool(poolInfo, null);
        }

        _descriptorSets = new VkDescriptorSet[1];

        Span<VkDescriptorSetLayout> layouts = [_descriptorSetLayout];
        fixed (VkDescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new VkDescriptorSetAllocateInfo
            {
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                SetLayouts = layoutsPtr
            };

            _device.AllocateDescriptorSets(allocInfo, _descriptorSets);
        }
    }

    private unsafe void UpdateTextureDescriptor(TextureId textureId)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = _textureSampler,
            ImageView = _textureManager.GetTexture(textureId).ImageView.VkImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
        [
            new()
            {
                DstSet = _descriptorSets[0],
                DstBinding = 0,
                DstArrayElement = (Int32)textureId.Value,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageInfo
            }
        ];
        _device.UpdateDescriptorSets(descriptorWrites, null);
    }

    private void CreateTextureSampler()
    {
        var samplerInfo = new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.ClampToEdge,
            AddressModeV = VkSamplerAddressMode.ClampToEdge,
            AddressModeW = VkSamplerAddressMode.ClampToEdge,
            AnisotropyEnable = true,
            MaxAnisotropy = _physicalDevice.GetProperties().Limits.MaxSamplerAnisotropy,
            CompareEnable = false,
            CompareOp = VkCompareOp.Always,
            BorderColor = VkBorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 1000.0f
        };

        _textureSampler = _device.CreateSampler(samplerInfo, null);
    }

    public static void CopyBufferToImage(CommandBuffer commandBuffer, VkBuffer buffer, ImageEx image, Int32 width,
        Int32 height)
    {
        var region = new VkBufferImageCopy
        {
            BufferOffset = new ByteSize64(0),
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
                { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = 0, BaseArrayLayer = 0, LayerCount = 1 },
            ImageOffset = new VkOffset3D(0, 0, 0),
            ImageExtent = { Width = width, Height = height, Depth = 1 }
        };
        commandBuffer.CopyBufferToImage(buffer, image.VkImage, VkImageLayout.TransferDstOptimal, [region]);
    }

    private static Byte[] LoadShader(String filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shaders", filename);

        return File.ReadAllBytes(path);
    }

    public void Dispose()
    {
        _device.WaitIdle();

        _imGuiManager.Dispose();

        _renderer.Dispose();
        _pipelineManager.Dispose();
        _device.DestroyDescriptorPool(_descriptorPool, null);
        _device.DestroyDescriptorSetLayout(_descriptorSetLayout, null);
        _textureManager.Dispose();
        _meshManager.Dispose();
        _device.DestroySampler(_textureSampler, null);
        _device.DestroyCommandPool(_commandPool, null);
        _device.DestroyVmaAllocator(_allocator);
        _instance.DestroySurfaceKHR(_surface, null);

        _device.Dispose();
        _instance.Dispose();

        _inputManager.Dispose();

        _window.OnFramebufferSize -= OnFramebufferSize;
        _window.Dispose();
        _gcHandle.Free();

        Glfw.Terminate();
    }
}