using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

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

    private MeshId[] _meshIds;
    private Transform[] _transforms;
    private MaterialId[] _materialIds;

    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;
    private ModelLoader _modelLoader;
    private MeshManager _meshManager;
    private MaterialManager _materialManager;
    private Renderer _renderer;

    public App()
    {
        InitGlfw();

        _window = Glfw.CreateWindow(
            new GlfwWindow.CreateInfo
                { Title = "Brutal Vulkan", Size = new int2(Constants.WindowHeight, Constants.WindowHeight) }
        );
        Glfw.PInvoke.SetInputMode(_window, (int)GlfwInputMode.Cursor, (int)GlfwCursorMode.Disabled);
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

        app._renderer.SetFramebufferResized();
        app._camera.AspectRatio = (float)size.X / size.Y;
    }

    private unsafe void InitVulkan()
    {
        _ = Vulkan.GetInstanceProcAddr;

        var instanceExtensions = new HashSet<string>();
        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions);
        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        for (var i = 0; i < glfwExtensionCount; i++)
            instanceExtensions.Add(Utf8.GetString(glfwExtensions[i]));

        var layers = new HashSet<string>();
        VulkanHelpers.AddValidationLayers(layers);

        _instance = Vulkan.CreateInstance(
            new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = (int)VulkanHelpers.Api.VERSION_1_3 },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = instanceExtensions,
                EnabledLayers = layers,
            }
        );

        if (Glfw.CreateWindowSurface(_instance.VkHandle, _window, out var surfaceHandle) != (int)VkResult.Success)
            throw new InvalidOperationException("Failed to create a Vulkan surface for the window");
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

        var deviceExtensions = new HashSet<string>();
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

        _allocator = _device.CreateVmaAllocator(
            new VmaAllocatorCreateInfo
            {
                Instance = _instance,
                PhysicalDevice = _physicalDevice,
                Device = _device,
                VulkanApiVersion = (uint)VulkanHelpers.Api.VERSION_1_3
            }
        );

        CreateDescriptorSets();

        CreateTextureSampler();

        _renderer = new Renderer(_device, _physicalDevice, _surface, _window, _allocator, _commandPool, _queue,
            _pipelineManager, _descriptorSets);

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
            var dt = float.Min((float)Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.1f);
            last = now;

            _inputManager.Update();

            Glfw.PollEvents();

            if (_inputManager.IsKeyPressed(GlfwKey.Escape))
                _window.ShouldClose = true;

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
            _camera.Rotate((float)mouseDelta.X * sensitivity, -(float)mouseDelta.Y * sensitivity);

            var renderObjects = new List<RenderObject>();
            for (int i = 0; i < _meshIds.Length; i++)
            {
                renderObjects.Add(new RenderObject
                {
                    Mesh = _meshManager.GetGpuMesh(_meshIds[i]),
                    ModelMatrix = _transforms[i].GetModelMatrix(),
                    Material = _materialManager.GetMaterial(_materialIds[i])
                });
            }

            _renderer.DrawFrame(renderObjects.ToArray(), _camera.GetView() * _camera.GetProjection());
        }
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

        VkShaderModule CompileShader(ReadOnlySpan<byte> glsl, ShaderKind kind)
        {
            using var spirv = compiler.Compile(glsl, kind, "shader"u8, "main"u8, options);
            return ShaderModuleExtensions.FromSpirv(
                _device,
                new ReadOnlySpan<byte>(spirv.GetBytes(), (int)spirv.GetLength()),
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

            VkFormat format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<uint>(),
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

            var lineRasterizationState = new VkPipelineRasterizationStateCreateInfo
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
                RasterizationStateCreateInfo = lineRasterizationState,
                MultisampleStateCreateInfo = multisampleState,
                DepthStencilStateCreateInfo = depthStencil,
                DescriptorSetLayouts = [_descriptorSetLayout],
                PushConstantRanges = pushConstantRanges.ToArray().ToImmutableArray()
            };

            _pipelineManager.AddPipeline("Opaque", pipelineDescription);
        }

        // Pipeline 2
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
                            LoadShader("line.frag"),
                            ShaderKind.FragmentShader
                        )
                    )
                ],
                null
            );

            var vertexInput = new VertexInput(1, 3).AddBindingWithAttributes<Vertex>(0, 0, VkVertexInputRate.Vertex);

            VkFormat format = _renderer.SwapchainFormat.Format;

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
                    Size = ByteSize.Of<uint>(),
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

            var lineRasterizationState = new VkPipelineRasterizationStateCreateInfo
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
                RasterizationStateCreateInfo = lineRasterizationState,
                MultisampleStateCreateInfo = multisampleState,
                DepthStencilStateCreateInfo = depthStencil,
                DescriptorSetLayouts = [_descriptorSetLayout],
                PushConstantRanges = pushConstantRanges.ToArray().ToImmutableArray()
            };

            _pipelineManager.AddPipeline("Wireframe", pipelineDescription);
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
        
        var commandBuffer = BeginSingleTimeCommands();

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

        EndSingleTimeCommands(commandBuffer);
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
            new Material
            {
                TextureId = _textureManager.GetTextureId("brick"), PipelineId = _pipelineManager.GetPipelineId("Opaque")
            });
        _materialManager.AddMaterial("statue",
            new Material
            {
                TextureId = _textureManager.GetTextureId("statue"),
                PipelineId = _pipelineManager.GetPipelineId("Opaque")
            });
        _materialManager.AddMaterial("viking_room",
            new Material
            {
                TextureId = _textureManager.GetTextureId("viking_room"),
                PipelineId = _pipelineManager.GetPipelineId("Opaque")
            });
        _materialManager.AddMaterial("wireframe",
            new Material { TextureId = TextureId.Invalid, PipelineId = _pipelineManager.GetPipelineId("Wireframe") });
    }

    private void CreateObjects()
    {
        var numObjects = 4;

        _meshIds = new MeshId[numObjects];
        _transforms = new Transform[numObjects];
        _materialIds = new MaterialId[numObjects];

        // object 1
        {
            var objectIndex = 0;

            _meshIds[objectIndex] = _meshManager.GetMeshId("plane");
            _transforms[objectIndex] = new Transform
            {
                Position = new float3(0.0f, -2.5f, 0.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitX, float.DegreesToRadians(-90.0f)),
                Scale = new float3(10.0f, 10.0f, 1.0f)
            };
            _materialIds[objectIndex] = _materialManager.GetMaterialId("brick");
        }

        // object 2
        {
            var objectIndex = 1;

            _meshIds[objectIndex] = _meshManager.GetMeshId("cube");
            _transforms[objectIndex] = Transform.Identity;
            _transforms[objectIndex].Position = new float3(0.0f, 5.0f, -1.0f);
            _materialIds[objectIndex] = _materialManager.GetMaterialId("wireframe");
        }

        // object 3
        {
            var objectIndex = 2;

            _meshIds[objectIndex] = _meshManager.GetMeshId("viking_room");
            _transforms[objectIndex] = Transform.Identity;
            _transforms[objectIndex].Position = new float3(0.0f, 1.0f, 0.0f);
            _transforms[objectIndex].RotateWorld(float3.UnitY, float.DegreesToRadians(-90f));
            _materialIds[objectIndex] = _materialManager.GetMaterialId("viking_room");
        }

        // object 4
        {
            var objectIndex = 3;

            _meshIds[objectIndex] = _meshManager.GetMeshId("planet");
            _transforms[objectIndex] = Transform.Identity;
            _transforms[objectIndex].Position = new float3(3.0f, 1.0f, -2.0f);
            _transforms[objectIndex].RotateWorld(float3.UnitY, float.DegreesToRadians(-90f));
            _materialIds[objectIndex] = _materialManager.GetMaterialId("statue");
        }
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
        int mipLevels = 1,
        int baseMipLevel = 0
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
            new VkDescriptorSetLayoutBinding()
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
            new VkDescriptorPoolSize
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
            new VkWriteDescriptorSet()
            {
                DstSet = _descriptorSets[0],
                DstBinding = 0,
                DstArrayElement = (int)textureId.Value,
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

    private CommandBuffer BeginSingleTimeCommands()
    {
        var buffer = _device.AllocateCommandBuffer(
            new VkCommandBufferAllocateInfo
            {
                CommandPool = _commandPool,
                Level = VkCommandBufferLevel.Primary
            }
        );
        buffer.Begin();

        return buffer;
    }

    private void EndSingleTimeCommands(CommandBuffer buffer)
    {
        buffer.End();

        _queue.Submit(
            null,
            null,
            [buffer],
            null,
            VkFence.NullHandle
        );
        _queue.WaitIdle();
    }

    public static void CopyBufferToImage(CommandBuffer commandBuffer, VkBuffer buffer, ImageEx image, int width,
        int height)
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

    private static byte[] LoadShader(string filename)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "shaders", filename);

        return File.ReadAllBytes(path);
    }

    public void Dispose()
    {
        _device.WaitIdle();

        _renderer.Dispose();
        _device.DestroyDescriptorPool(_descriptorPool, null);
        _device.DestroyDescriptorSetLayout(_descriptorSetLayout, null);
        _textureManager.Dispose();
        _meshManager.Dispose();
        _device.DestroySampler(_textureSampler, null);
        _device.DestroyCommandPool(_commandPool, null);
        _pipelineManager.Dispose();
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