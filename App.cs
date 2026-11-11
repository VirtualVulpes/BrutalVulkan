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
    private bool _isFramebufferResized = false;

    private readonly InputManager _inputManager;
    private readonly Camera _camera;

    private Instance _instance;
    private PhysicalDevice? _physicalDevice = null;
    private DeviceEx _device;
    private VkSwapchainKHR _swapchain;
    private VkFormat _swapchainFormat;
    private int _queueFamily = -1;
    private Queue _queue;
    private VkExtent2D _extent;
    private Pipeline _pipeline;
    private VkSurfaceKHR _surface;
    private VmaAllocator _allocator;
    private VkImage[] _swapchainImages;
    private VkImageView[] _swapchainImageViews;
    private VkCommandPool _commandPool;

    private ImageEx _depthImage;
    private ImageViewEx _depthImageView;

    private ImageEx _colorImage;
    private ImageViewEx _colorImageView;

    private VkSampler _textureSampler;

    private VkSampleCountFlags _msaaSamples = VkSampleCountFlags._1Bit;

    private Mesh[] _meshes;
    private Transform[] _transforms;
    private Material[] _materials;

    private GpuMesh[] _gpuMeshes;

    private VkDescriptorSetLayout _descriptorSetLayout;
    private VkDescriptorPool _descriptorPool;
    private VkDescriptorSet[] _descriptorSets;

    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;

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

        app._isFramebufferResized = true;
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

        _msaaSamples = GetMaxSampleCount();

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

        CreateSwapchain();

        _swapchainImages = new VkImage[_device.GetSwapchainImagesKHR_Count(_swapchain)];
        _device.GetSwapchainImagesKHR_Write(_swapchain, _swapchainImages);

        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        CreateSwapchainImageViews();

        _allocator = _device.CreateVmaAllocator(
            new VmaAllocatorCreateInfo
            {
                Instance = _instance,
                PhysicalDevice = _physicalDevice,
                Device = _device,
                VulkanApiVersion = (uint)VulkanHelpers.Api.VERSION_1_3
            }
        );

        _textureManager = new TextureManager(_device);

        _commandPool = _device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = _queueFamily,
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit
            },
            null
        );

        CreateColorResources();
        CreateDepthResources();

        CreateDescriptorSets();

        CreateTextureSampler();

        var commandBuffer = BeginSingleTimeCommands();
        _textureManager.CreateTexture("brick.ktx2", ref commandBuffer, ref _allocator);
        UpdateTextureDescriptor(_textureManager.Get("brick"));
        _textureManager.CreateTexture("texture.ktx2", ref commandBuffer, ref _allocator);
        UpdateTextureDescriptor(_textureManager.Get("texture"));
        EndSingleTimeCommands(ref commandBuffer);
        _textureManager.ClearStagingBuffers();

        CreatePipelines();
    }

    public unsafe void Run()
    {
        var commandBuffer = _device.AllocateCommandBuffer(
            new VkCommandBufferAllocateInfo
            {
                CommandPool = _commandPool,
                Level = VkCommandBufferLevel.Primary
            }
        );

        var imageAvailable = _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        var renderFinished = _swapchainImages.Select(_ => _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null))
            .ToArray();
        var frameFence = _device.CreateFence(VkFenceCreateFlags.SignaledBit, null);

        CreateObjects();

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

            if (_inputManager.IsKeyPressed(GlfwKey.Z)) _pipeline = _pipelineManager.Get("Wireframe");
            if (_inputManager.IsKeyPressed(GlfwKey.X)) _pipeline = _pipelineManager.Get("Opaque");

            _device.WaitForFence(frameFence, -1);
            _device.ResetFences([frameFence]);

            _device.AcquireNextImageKHR(_swapchain, -1, imageAvailable, VkFence.NullHandle, out var imageIndex);

            commandBuffer.Reset();
            commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

            TransitionImage(
                _swapchainImages[imageIndex],
                VkImageLayout.Undefined,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags.ColorAttachmentOutputBit,
                VkPipelineStageFlags.ColorAttachmentOutputBit,
                VkAccessFlags.None,
                VkAccessFlags.ColorAttachmentWriteBit,
                VkImageAspectFlags.ColorBit,
                commandBuffer
            );

            TransitionImage(
                _depthImage.VkImage,
                VkImageLayout.Undefined,
                VkImageLayout.DepthAttachmentOptimal,
                VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
                VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
                VkAccessFlags.DepthStencilAttachmentWriteBit,
                VkAccessFlags.DepthStencilAttachmentWriteBit,
                VkImageAspectFlags.DepthBit,
                commandBuffer
            );

            TransitionImage(
                _colorImage.VkImage,
                VkImageLayout.Undefined,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags.ColorAttachmentOutputBit,
                VkPipelineStageFlags.ColorAttachmentOutputBit,
                VkAccessFlags.ColorAttachmentWriteBit,
                VkAccessFlags.ColorAttachmentWriteBit,
                VkImageAspectFlags.ColorBit,
                commandBuffer
            );

            var colorAttachment = new VkRenderingAttachmentInfo
            {
                ImageView = _colorImageView.VkImageView,
                ImageLayout = VkImageLayout.ColorAttachmentOptimal,
                ResolveMode = VkResolveModeFlags.AverageBit,
                ResolveImageView = _swapchainImageViews[imageIndex],
                ResolveImageLayout = VkImageLayout.ColorAttachmentOptimal,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.Store,
                ClearValue = new VkClearColorValue { Float32 = new float4(0.01f, 0.01f, 0.01f, 1.0f) }
            };

            var depthAttachment = new VkRenderingAttachmentInfo
            {
                ImageView = _depthImageView.VkImageView,
                ImageLayout = VkImageLayout.DepthAttachmentOptimal,
                LoadOp = VkAttachmentLoadOp.Clear,
                StoreOp = VkAttachmentStoreOp.DontCare,
                ClearValue = new VkClearDepthStencilValue { Depth = 1.0f, Stencil = 0 }
            };

            commandBuffer.BeginRendering(
                new VkRenderingInfo
                {
                    RenderArea = new VkRect2D(_extent),
                    LayerCount = 1,
                    ColorAttachmentCount = 1,
                    ColorAttachments = &colorAttachment,
                    DepthAttachment = &depthAttachment
                }
            );

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline.Handle);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipeline.Layout, 0, _descriptorSets, null);

            commandBuffer.SetViewport(0, [
                new VkViewport
                {
                    X = 0.0f,
                    Y = _extent.Height,
                    Width = _extent.Width,
                    Height = -_extent.Height,
                    MinDepth = 0.0f,
                    MaxDepth = 1.0f
                }
            ]);
            commandBuffer.SetScissor(0, [new VkRect2D(_extent)]);

            var viewProj = _camera.GetView() * _camera.GetProjection();
            for (var i = 0; i < _meshes.Length; i++)
            {
                commandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.VertexBit, ByteSize.Zero,
                    _transforms[i].GetModelMatrix() * viewProj);
                commandBuffer.PushConstants(_pipeline.Layout, VkShaderStageFlags.FragmentBit, ByteSize.Of<float4x4>(),
                    _materials[i].TextureDescriptorIndex);

                commandBuffer.BindVertexBuffers(0, [_gpuMeshes[i].VertexBuffer], [new ByteSize64(0)]);
                commandBuffer.BindIndexBuffer(_gpuMeshes[i].IndexBuffer, new ByteSize64(0), VkIndexType.UInt32);
                commandBuffer.DrawIndexed(_gpuMeshes[i].IndexCount, 1, 0, 0, 0);
            }

            commandBuffer.EndRendering();

            TransitionImage(
                _swapchainImages[imageIndex],
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.PresentSrcKHR,
                VkPipelineStageFlags.ColorAttachmentOutputBit,
                VkPipelineStageFlags.BottomOfPipeBit,
                VkAccessFlags.ColorAttachmentWriteBit,
                VkAccessFlags.None,
                VkImageAspectFlags.ColorBit,
                commandBuffer
            );

            commandBuffer.End();

            var signalSemaphore = renderFinished[imageIndex];
            _queue.Submit(
                [imageAvailable],
                [VkPipelineStageFlags.ColorAttachmentOutputBit],
                [commandBuffer],
                [signalSemaphore],
                frameFence
            );

            var swapchain = _swapchain;

            _queue.PresentKHR(
                new VkPresentInfoKHR
                {
                    WaitSemaphoreCount = 1,
                    WaitSemaphores = &signalSemaphore,
                    SwapchainCount = 1,
                    Swapchains = &swapchain,
                    ImageIndices = &imageIndex
                }
            );

            if (_isFramebufferResized)
            {
                _isFramebufferResized = false;
                RecreateSwapchain();
            }
        }

        _device.WaitIdle();

        foreach (var mesh in _gpuMeshes)
        {
            mesh.Dispose();
        }

        Array.Clear(_gpuMeshes);


        _device.DestroyDescriptorPool(_descriptorPool, null);
        _device.DestroyDescriptorSetLayout(_descriptorSetLayout, null);
        _textureManager.Dispose();
        _device.DestroySampler(_textureSampler, null);
        _device.DestroyFence(frameFence, null);
        _device.DestroySemaphore(imageAvailable, null);
        foreach (var semaphore in renderFinished)
            _device.DestroySemaphore(semaphore, null);
        _device.DestroyCommandPool(_commandPool, null);
        _pipelineManager.Dispose();
        foreach (var imageView in _swapchainImageViews)
            _device.DestroyImageView(imageView, null);
        Array.Clear(_swapchainImageViews);
        _colorImage.Dispose();
        _colorImageView.Dispose();
        _depthImageView.Dispose();
        _depthImage.Dispose();
        _device.DestroySwapchainKHR(_swapchain, null);
        _device.DestroyVmaAllocator(_allocator);
        _instance.DestroySurfaceKHR(_surface, null);
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

            VkFormat format = _swapchainFormat;

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
                RasterizationSamples = _msaaSamples
            };

            var lineRasterizationState = new VkPipelineRasterizationStateCreateInfo
            {
                PolygonMode = VkPolygonMode.Fill,
                CullMode = VkCullModeFlags.BackBit,
                LineWidth = 1.0f
            };

            var pipelineDescription = new PipelineDescription
            {
                Id = "Opaque",
                RenderingCreateInfo = rendering,
                ShaderStages = shaderStages,
                VertexInput = vertexInput,
                RasterizationStateCreateInfo = lineRasterizationState,
                MultisampleStateCreateInfo = multisampleState,
                DepthStencilStateCreateInfo = depthStencil,
                DescriptorSetLayouts = [_descriptorSetLayout],
                PushConstantRanges = pushConstantRanges.ToArray().ToImmutableArray()
            };

            _pipelineManager.Create(pipelineDescription);
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

            VkFormat format = _swapchainFormat;

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
                RasterizationSamples = _msaaSamples
            };

            var lineRasterizationState = new VkPipelineRasterizationStateCreateInfo
            {
                PolygonMode = VkPolygonMode.Line,
                CullMode = VkCullModeFlags.BackBit,
                LineWidth = 1.0f
            };

            var pipelineDescription = new PipelineDescription
            {
                Id = "Wireframe",
                RenderingCreateInfo = rendering,
                ShaderStages = shaderStages,
                VertexInput = vertexInput,
                RasterizationStateCreateInfo = lineRasterizationState,
                MultisampleStateCreateInfo = multisampleState,
                DepthStencilStateCreateInfo = depthStencil,
                DescriptorSetLayouts = [_descriptorSetLayout],
                PushConstantRanges = pushConstantRanges.ToArray().ToImmutableArray()
            };

            _pipelineManager.Create(pipelineDescription);
        }

        _pipeline = _pipelineManager.Get("Wireframe");
    }

    private void CreateObjects()
    {
        var numObjects = 3;

        _meshes = new Mesh[numObjects];
        _transforms = new Transform[numObjects];
        _materials = new Material[numObjects];

        _gpuMeshes = new GpuMesh[numObjects];

        // mesh 1
        {
            var meshIndex = 0;

            _meshes[meshIndex] = new Mesh(
                [
                    new Vertex(new float3(-0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 1.0f)),
                    new Vertex(new float3(0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 1.0f)),
                    new Vertex(new float3(0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 0.0f)),
                    new Vertex(new float3(-0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 0.0f))
                ],
                [
                    0, 1, 2, 2, 3, 0
                ]
            );
            _transforms[meshIndex] = new Transform
            {
                Position = new float3(0.0f, -2.5f, 0.0f),
                Rotation = floatQuat.CreateFromAxisAngle(float3.UnitX, float.DegreesToRadians(-90.0f)),
                Scale = new float3(10.0f, 10.0f, 1.0f)
            };
            _materials[meshIndex] = new Material
            {
                TextureDescriptorIndex = _textureManager.Get("brick").DescriptorIndex
            };
            _gpuMeshes[meshIndex] = new GpuMesh(_allocator, _meshes[0]);
        }

        // mesh 2
        {
            var meshIndex = 1;

            _meshes[meshIndex] = new Mesh(
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
            );
            _transforms[meshIndex] = Transform.Identity;
            _transforms[meshIndex].Position = new float3(0.0f, 5.0f, -1.0f);
            _materials[meshIndex] = new Material
            {
                TextureDescriptorIndex = _textureManager.Get("texture").DescriptorIndex
            };
            _gpuMeshes[meshIndex] = new GpuMesh(_allocator, _meshes[1]);
        }

        // mesh 3
        {
            var meshIndex = 2;

            CubeSphere.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
            _meshes[meshIndex] = CubeSphere.CreateMesh(3, 2.0f);
            _transforms[meshIndex] = Transform.Identity;
            _transforms[meshIndex].Position = new float3(0.0f, 1.0f, 0.0f);
            _materials[meshIndex] = new Material
            {
                TextureDescriptorIndex = _textureManager.Get("texture").DescriptorIndex
            };
            _gpuMeshes[meshIndex] = new GpuMesh(_allocator, _meshes[2]);
        }
    }

    private void RecreateSwapchain()
    {
        int2 size = _window.FramebufferSize;
        while ((size.X == 0 || size.Y == 0) && !_window.ShouldClose)
        {
            size = _window.FramebufferSize;
            Glfw.WaitEvents();
        }

        if (_window.ShouldClose)
            return;

        _device.WaitIdle();

        CleanupSwapchain();
        CreateSwapchain();
        CreateSwapchainImageViews();
        CreateColorResources();
        CreateDepthResources();
    }

    private void CleanupSwapchain()
    {
        foreach (var imageView in _swapchainImageViews)
            _device.DestroyImageView(imageView, null);
        Array.Clear(_swapchainImageViews);

        _depthImageView.Dispose();
        _depthImage.Dispose();

        _colorImage.Dispose();
        _colorImageView.Dispose();

        _device.DestroySwapchainKHR(_swapchain, null);
        _swapchain = VkSwapchainKHR.NullHandle;
    }

    private void CreateSwapchain()
    {
        var capabilities = _physicalDevice.GetSurfaceCapabilitiesKHR(_surface);

        _extent = capabilities.CurrentExtent;
        if (_extent.Width == -1 && _extent.Height == -1)
            _extent = new VkExtent2D(
                Math.Clamp(_window.FramebufferSize.X, capabilities.MinImageExtent.Width,
                    capabilities.MaxImageExtent.Width),
                Math.Clamp(_window.FramebufferSize.Y, capabilities.MinImageExtent.Height,
                    capabilities.MaxImageExtent.Height)
            );

        var imageCount = Math.Max(capabilities.MinImageCount, 2);
        if (capabilities.MaxImageCount > 0)
            imageCount = Math.Min(imageCount, capabilities.MaxImageCount);

        VkCompositeAlphaFlagsKHR[] alphaModes =
        [
            VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
            VkCompositeAlphaFlagsKHR.PreMultipliedBitKHR,
            VkCompositeAlphaFlagsKHR.PostMultipliedBitKHR,
            VkCompositeAlphaFlagsKHR.InheritBitKHR
        ];
        var compositeAlpha = alphaModes.First(mode => (capabilities.SupportedCompositeAlpha & mode) != 0);

        Span<VkSurfaceFormatKHR> surfaceFormats =
            stackalloc VkSurfaceFormatKHR[_physicalDevice.GetSurfaceFormatsKHR_Count(_surface)];
        _physicalDevice.GetSurfaceFormatsKHR_Write(_surface, surfaceFormats);
        _swapchainFormat = surfaceFormats[0].Format;

        _swapchain = _device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = _swapchainFormat,
                ImageColorSpace = surfaceFormats[0].ColorSpace,
                ImageExtent = _extent,
                ImageArrayLayers = 1,
                ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = VkSharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = compositeAlpha,
                PresentMode = VkPresentModeKHR.FifoKHR,
                Clipped = true
            },
            null
        );
        _device.GetSwapchainImagesKHR_Write(_swapchain, _swapchainImages);
    }

    private void CreateSwapchainImageViews()
    {
        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            _swapchainImageViews[i] = _device.CreateImageView(
                new VkImageViewCreateInfo
                {
                    Image = _swapchainImages[i],
                    ViewType = VkImageViewType._2D,
                    Format = _swapchainFormat,
                    SubresourceRange = new VkImageSubresourceRange
                    {
                        AspectMask = VkImageAspectFlags.ColorBit,
                        LevelCount = 1,
                        LayerCount = 1
                    }
                },
                null
            );
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

    private unsafe void UpdateTextureDescriptor(Texture texture)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = _textureSampler,
            ImageView = texture.ImageView.VkImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
        [
            new VkWriteDescriptorSet()
            {
                DstSet = _descriptorSets[0],
                DstBinding = 0,
                DstArrayElement = texture.DescriptorIndex,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageInfo
            }
        ];
        _device.UpdateDescriptorSets(descriptorWrites, null);
    }

    private void CreateColorResources()
    {
        var colorInfo = new ImageEx.CreateInfo
        {
            Name = "Color Image",
            ImageType = VkImageType._2D,
            ImageFormat = _swapchainFormat,
            ImageExtent = new VkExtent3D
            {
                Width = _extent.Width,
                Height = _extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = _msaaSamples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };
        _colorImage = _allocator.CreateImage(colorInfo);
        var colorSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LayerCount = 1,
            LevelCount = 1
        };
        _colorImageView = _colorImage.CreateImageView(VkImageViewType._2D, colorSubresourceRange,
            _colorImage.Allocator.VkAllocator);
    }

    private void CreateDepthResources()
    {
        var depthInfo = new ImageEx.CreateInfo
        {
            Name = "Depth Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.D32SFloat,
            ImageExtent = new VkExtent3D
            {
                Width = _extent.Width,
                Height = _extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = _msaaSamples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.DepthStencilAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };
        _depthImage = _allocator.CreateImage(depthInfo);
        var depthSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.DepthBit,
            LayerCount = 1,
            LevelCount = 1
        };
        _depthImageView = _depthImage.CreateImageView(VkImageViewType._2D, depthSubresourceRange,
            _depthImage.Allocator.VkAllocator);
    }

    private VkSampleCountFlags GetMaxSampleCount()
    {
        var properties = _physicalDevice.GetProperties();

        var counts = properties.Limits.FramebufferColorSampleCounts &
                     properties.Limits.FramebufferDepthSampleCounts;

        if ((int)(counts & VkSampleCountFlags._64Bit) != 0) return VkSampleCountFlags._64Bit;
        if ((int)(counts & VkSampleCountFlags._32Bit) != 0) return VkSampleCountFlags._32Bit;
        if ((int)(counts & VkSampleCountFlags._16Bit) != 0) return VkSampleCountFlags._16Bit;
        if ((int)(counts & VkSampleCountFlags._8Bit) != 0) return VkSampleCountFlags._8Bit;
        if ((int)(counts & VkSampleCountFlags._4Bit) != 0) return VkSampleCountFlags._4Bit;
        if ((int)(counts & VkSampleCountFlags._2Bit) != 0) return VkSampleCountFlags._2Bit;

        return VkSampleCountFlags._1Bit;
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

    private void EndSingleTimeCommands(ref CommandBuffer buffer)
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

    public static void CopyBufferToImage(ref CommandBuffer commandBuffer, VkBuffer buffer, ref ImageEx image, int width,
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
        _device.Dispose();
        _instance.Dispose();

        _inputManager.Dispose();

        _window.OnFramebufferSize -= OnFramebufferSize;
        _window.Dispose();
        _gcHandle.Free();

        Glfw.Terminate();
    }
}