using Brutal;
using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.Rendering.GpuTypes;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Passes;
using BrutalVulkan.Rendering.Passes.Compute;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.UI;
using BrutalVulkan.Utility;
using CommunityToolkit.HighPerformance;

namespace BrutalVulkan.Rendering;

public sealed class Renderer : IDisposable
{
    private readonly DeviceEx _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly VkSurfaceKHR _surface;
    private readonly GlfwWindow _window;
    private readonly VmaAllocator _allocator;
    private readonly Queue _queue;
    private readonly VkCommandPool _commandPool;

    private DescriptorManager _descriptorManager;
    private ImGuiManager _imGuiManager;
    private MeshManager _meshManager;
    private PipelineManager _pipelineManager;

    private readonly FrameResources[] _frameResources = new FrameResources[Constants.MaxFramesInFlight];

    private VkSemaphore[] _renderFinished;

    private VkSwapchainKHR _swapchain;
    private VkImage[] _swapchainImages;
    private VkImageView[] _swapchainImageViews;

    private Boolean _isFramebufferResized;
    private Int32 _frameIndex;

    private readonly List<RenderItem> _renderItems = [];

    private BufferEx _lightsBuffer;
    private BufferEx _cameraBuffer;

    private SelectionMaskPass _selectionMaskPass;
    private OpaquePass _opaquePass;
    private AtmospherePass _atmospherePass;
    private OutlineCompositePass _outlineCompositePass;
    private ImGuiPass _imGuiPass;

    private SeedInitPass _seedInitPass;
    private JumpFloodPass _jumpFloodPass;
    private EntityIdPass _entityIdPass;

    private EntityPicker _picker;
    public EntityPicker Picker => _picker;

    public VkExtent2D Extent { get; private set; }

    public VkSampleCountFlags MsaaSamples { get; }

    public VkSurfaceFormatKHR SwapchainFormat { get; private set; }

    public Renderer(DeviceEx device, PhysicalDevice physicalDevice, VkSurfaceKHR surface,
        GlfwWindow window, VmaAllocator allocator, VkCommandPool commandPool, Queue queue)
    {
        _device = device;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _window = window;
        _allocator = allocator;
        _commandPool = commandPool;
        _queue = queue;

        MsaaSamples = (VkSampleCountFlags)Int32.Min(Constants.DefaultMsaa, (Int32)GetMaxSampleCount());

        for (var i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _frameResources[i].ImageAvailable = _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
            _frameResources[i].Fence = _device.CreateFence(VkFenceCreateFlags.SignaledBit, null);
            _frameResources[i].CommandBuffer = _device.AllocateCommandBuffer(
                new VkCommandBufferAllocateInfo
                {
                    CommandPool = _commandPool,
                    Level = VkCommandBufferLevel.Primary
                }
            );
        }

        CreateSwapchain();
    }

    public void Initialize(PipelineManager pipelineManager, DescriptorManager descriptorManager,
        ImGuiManager imGuiManager,
        MeshManager meshManager, InputManager inputManager)
    {
        _descriptorManager = descriptorManager;
        _imGuiManager = imGuiManager;
        _meshManager = meshManager;
        _pipelineManager = pipelineManager;

        _picker = new EntityPicker(_device, inputManager);

        _selectionMaskPass = new SelectionMaskPass(_pipelineManager.GetPipeline("Selection Mask"));
        _opaquePass = new OpaquePass();
        _atmospherePass = new AtmospherePass(_pipelineManager.GetPipeline("Atmosphere"));
        _outlineCompositePass = new OutlineCompositePass(_pipelineManager.GetPipeline("Outline Composite"));
        _imGuiPass = new ImGuiPass(_imGuiManager);

        _seedInitPass = new SeedInitPass(_pipelineManager.GetPipeline("Seed Init"));
        _jumpFloodPass = new JumpFloodPass(_pipelineManager.GetPipeline("Jump Flood"));
        _entityIdPass = new EntityIdPass(_pipelineManager.GetPipeline("Entity Id"));

        CreateBuffers();
        UpdateLightDescriptor();
        UpdateCameraDescriptor();

        CreateResources();
    }

    private void CreateBuffers()
    {
        var lightBuffer = new BufferEx.CreateInfo
        {
            Name = "Lights Storage Buffer",
            BufferSize = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights,
            BufferUsage = VkBufferUsageFlags.StorageBufferBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };
        _lightsBuffer = _device.CreateBuffer(lightBuffer);

        var cameraBuffer = new BufferEx.CreateInfo
        {
            Name = "Camera Buffer",
            BufferSize = ByteSize.Of<GpuCamera>(),
            BufferUsage = VkBufferUsageFlags.UniformBufferBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };
        _cameraBuffer = _device.CreateBuffer(cameraBuffer);
    }

    private unsafe void UpdateLights(ReadOnlySpan<GpuLight> lights)
    {
        if (lights.Length > Constants.MaxLights)
            throw new ArgumentOutOfRangeException(nameof(lights));

        var count = lights.Length;

        var memory = _lightsBuffer.Map();

        try
        {
            var dst = (Byte*)memory.MapPtr.Pointer;

            Buffer.MemoryCopy(
                &count,
                dst,
                ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights,
                ByteSize.Of<UInt32>()
            );

            fixed (GpuLight* lightsPtr = lights)
            {
                Buffer.MemoryCopy(
                    lightsPtr,
                    dst + ByteSize.Of<UInt32>() * 4,
                    ByteSize.Of<GpuLight>() * Constants.MaxLights,
                    ByteSize.Of<GpuLight>() * lights.Length
                );
            }
        }
        finally
        {
            memory.Unmap();
        }
    }

    private unsafe void UpdateCamera(GpuCamera camera)
    {
        var memory = _cameraBuffer.Map();

        try
        {
            var dst = (Byte*)memory.MapPtr.Pointer;

            Buffer.MemoryCopy(
                &camera,
                dst,
                ByteSize.Of<GpuCamera>(),
                ByteSize.Of<GpuCamera>()
            );
        }
        finally
        {
            memory.Unmap();
        }
    }

    private unsafe void UpdateLightDescriptor()
    {
        var bufferInfo = new VkDescriptorBufferInfo
        {
            Buffer = _lightsBuffer.VkBuffer,
            Offset = ByteSize.Zero,
            Range = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights
        };

        ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
        [
            new()
            {
                DstSet = _descriptorManager.GlobalSet,
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.StorageBuffer,
                BufferInfo = &bufferInfo
            }
        ];

        _device.UpdateDescriptorSets(descriptorWrites, null);
    }

    private unsafe void UpdateCameraDescriptor()
    {
        var bufferInfo = new VkDescriptorBufferInfo
        {
            Buffer = _cameraBuffer.VkBuffer,
            Offset = ByteSize.Zero,
            Range = ByteSize.Of<GpuCamera>()
        };

        ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
        [
            new()
            {
                DstSet = _descriptorManager.GlobalSet,
                DstBinding = 2,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.UniformBuffer,
                BufferInfo = &bufferInfo
            }
        ];

        _device.UpdateDescriptorSets(descriptorWrites, null);
    }

    private void CreateSwapchain()
    {
        var capabilities = _physicalDevice.GetSurfaceCapabilitiesKHR(_surface);

        Extent = capabilities.CurrentExtent;
        if (Extent is { Width: -1, Height: -1 })
            Extent = new VkExtent2D(
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

        SwapchainFormat = ChooseSurfaceFormat();

        _swapchain = _device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = SwapchainFormat.Format,
                ImageColorSpace = SwapchainFormat.ColorSpace,
                ImageExtent = Extent,
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
    }

    private VkSurfaceFormatKHR ChooseSurfaceFormat()
    {
        Span<VkSurfaceFormatKHR> surfaceFormats =
            stackalloc VkSurfaceFormatKHR[_physicalDevice.GetSurfaceFormatsKHR_Count(_surface)];
        _physicalDevice.GetSurfaceFormatsKHR_Write(_surface, surfaceFormats);

        if (surfaceFormats.IsEmpty)
            throw new InvalidOperationException("No surface formats available.");

        foreach (var format in surfaceFormats)
        {
            if (format is { Format: VkFormat.B8G8R8A8SRGB, ColorSpace: VkColorSpaceKHR.SRGBNonlinearKHR })
                return format;
        }

        return surfaceFormats[0];
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
                    Format = SwapchainFormat.Format,
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

    private void CleanupSwapchain()
    {
        foreach (var semaphore in _renderFinished)
            _device.DestroySemaphore(semaphore, null);
        foreach (var imageView in _swapchainImageViews)
            _device.DestroyImageView(imageView, null);
        Array.Clear(_swapchainImageViews);

        foreach (var resource in _frameResources)
        {
            resource.ColorImageView.Dispose();
            resource.ColorImage.Dispose();

            resource.DepthImageView.Dispose();
            resource.DepthImage.Dispose();

            resource.ResolveColorImageView.Dispose();
            resource.ResolveColorImage.Dispose();
            
            resource.PostProcessImageView.Dispose();
            resource.PostProcessImage.Dispose();

            resource.SelectionMaskImageView.Dispose();
            resource.SelectionMaskImage.Dispose();

            resource.SeedAImageView.Dispose();
            resource.SeedAImage.Dispose();

            resource.SeedBImageView.Dispose();
            resource.SeedBImage.Dispose();

            resource.EntityIdImageView.Dispose();
            resource.EntityIdImage.Dispose();

            resource.EntityIdDepthImageView.Dispose();
            resource.EntityIdDepthImage.Dispose();
        }

        _device.DestroySwapchainKHR(_swapchain, null);
        _swapchain = VkSwapchainKHR.NullHandle;
    }

    private void RecreateSwapchain()
    {
        var size = _window.FramebufferSize;
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

        CreateResources();

        _imGuiManager.UpdateDisplaySize();
        _picker.Reset();
    }

    private void CreateResources()
    {
        CreateSwapchainResources();
        CreateColorResources();
        CreateDepthResources();
        CreateEntityIdResources();
        CreateSelectionMaskResources();
        CreatePostProcessResources();
        CreateOutlineResources();
        CreateAtmosphereResources();
    }

    private void CreateSwapchainResources()
    {
        _swapchainImages = new VkImage[_device.GetSwapchainImagesKHR_Count(_swapchain)];
        _device.GetSwapchainImagesKHR_Write(_swapchain, _swapchainImages);

        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        CreateSwapchainImageViews();

        _renderFinished = [.. _swapchainImages.Select(_ => _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null))];
    }

    private void CreateColorResources()
    {
        var colorInfo = new ImageEx.CreateInfo
        {
            Name = "Color Image",
            ImageType = VkImageType._2D,
            ImageFormat = SwapchainFormat.Format,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = MsaaSamples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };

        var resolveColorInfo = new ImageEx.CreateInfo
        {
            Name = "Resolve Color Image",
            ImageType = VkImageType._2D,
            ImageFormat = SwapchainFormat.Format,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };

        var colorSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LayerCount = 1,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
        {
            var colorImage = _allocator.CreateImage(colorInfo);
            _frameResources[i].ColorImage = new TrackedImage(colorImage);
            _frameResources[i].ColorImageView = colorImage.CreateImageView(
                VkImageViewType._2D, colorSubresourceRange, colorImage.Allocator.VkAllocator);

            var resolveColorImage = _allocator.CreateImage(resolveColorInfo);
            _frameResources[i].ResolveColorImage = new TrackedImage(resolveColorImage);
            _frameResources[i].ResolveColorImageView = resolveColorImage.CreateImageView(
                VkImageViewType._2D, colorSubresourceRange, resolveColorImage.Allocator.VkAllocator);
        }
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
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = MsaaSamples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.DepthStencilAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };

        var depthSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.DepthBit,
            LayerCount = 1,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
        {
            var depthImage = _allocator.CreateImage(depthInfo);
            _frameResources[i].DepthImage = new TrackedImage(depthImage);
            _frameResources[i].DepthImageView = depthImage.CreateImageView(
                VkImageViewType._2D, depthSubresourceRange, depthImage.Allocator.VkAllocator);
        }
    }

    private void CreateEntityIdResources()
    {
        var createInfo = new ImageEx.CreateInfo
        {
            Name = "Entity Id Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.R32UInt,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.StorageBit |
                         VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };

        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        };

        var depthInfo = new ImageEx.CreateInfo
        {
            Name = "Entity Id Depth Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.D32SFloat,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
        };

        var depthSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.DepthBit,
            LayerCount = 1,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var resources = ref _frameResources[i];

            var image = _allocator.CreateImage(createInfo);
            resources.EntityIdImage = new TrackedImage(image);
            resources.EntityIdImageView = image.CreateImageView(
                VkImageViewType._2D, subresourceRange, image.Allocator.VkAllocator);

            var depthImage = _allocator.CreateImage(depthInfo);
            resources.EntityIdDepthImage = new TrackedImage(depthImage);
            resources.EntityIdDepthImageView = depthImage.CreateImageView(
                VkImageViewType._2D, depthSubresourceRange, depthImage.Allocator.VkAllocator);
        }
    }

    private void CreateSelectionMaskResources()
    {
        var createInfo = new ImageEx.CreateInfo
        {
            Name = "Selection Mask Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.R8UNorm,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit |
                         VkImageUsageFlags.SampledBit | VkImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };

        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var resources = ref _frameResources[i];

            var image = _allocator.CreateImage(createInfo);

            resources.SelectionMaskImage = new TrackedImage(image);
            resources.SelectionMaskImageView = image.CreateImageView(
                VkImageViewType._2D, subresourceRange, image.Allocator.VkAllocator);
        }
    }

    private void CreateOutlineResources()
    {
        var createInfo = new ImageEx.CreateInfo
        {
            Name = "Seed Init Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.R32G32SInt,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };

        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var resources = ref _frameResources[i];

            var imageA = _allocator.CreateImage(createInfo);

            resources.SeedAImage = new TrackedImage(imageA);
            resources.SeedAImageView = imageA.CreateImageView(
                VkImageViewType._2D, subresourceRange, imageA.Allocator.VkAllocator);

            var imageB = _allocator.CreateImage(createInfo);

            resources.SeedBImage = new TrackedImage(imageB);
            resources.SeedBImageView = imageB.CreateImageView(
                VkImageViewType._2D, subresourceRange, imageB.Allocator.VkAllocator);

            if (resources.SeedDescriptorSet.IsNull())
                resources.SeedDescriptorSet = _descriptorManager.AllocateSeedDescriptorSet();

            if (resources.SeedAToBDescriptorSet.IsNull())
                resources.SeedAToBDescriptorSet = _descriptorManager.AllocateSeedAToBDescriptorSet();

            if (resources.SeedBToADescriptorSet.IsNull())
                resources.SeedBToADescriptorSet = _descriptorManager.AllocateSeedBToADescriptorSet();
            
            if (resources.OutlineCompositeDescriptorSet.IsNull())
                resources.OutlineCompositeDescriptorSet = _descriptorManager.AllocateOutlineDescriptorSet();
            
            _descriptorManager.UpdateSeedDescriptor(resources.SeedDescriptorSet,
                resources.SelectionMaskImageView.VkImageView, resources.SeedAImageView.VkImageView);

            _descriptorManager.UpdateSeedDescriptor(resources.SeedAToBDescriptorSet,
                resources.SeedAImageView.VkImageView, resources.SeedBImageView.VkImageView);

            _descriptorManager.UpdateSeedDescriptor(resources.SeedBToADescriptorSet,
                resources.SeedBImageView.VkImageView, resources.SeedAImageView.VkImageView);

            _descriptorManager.UpdateOutlineDescriptor(resources.OutlineCompositeDescriptorSet,
                resources.PostProcessImageView.VkImageView, resources.SeedAImageView.VkImageView);
        }
    }

    private void CreatePostProcessResources()
    {
        var createInfo = new ImageEx.CreateInfo
        {
            Name = "Post Process Image",
            ImageType = VkImageType._2D,
            ImageFormat = SwapchainFormat.Format,
            ImageExtent = new VkExtent3D
            {
                Width = Extent.Width,
                Height = Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };

        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        };
        
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var resources = ref _frameResources[i];

            resources.PostProcessImage = new TrackedImage(_allocator.CreateImage(createInfo));
            resources.PostProcessImageView = resources.PostProcessImage.Image.CreateImageView(VkImageViewType._2D, subresourceRange, null);
        }
    }
    
    private void CreateAtmosphereResources()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var resources = ref _frameResources[i];

            if (resources.AtmosphereDescriptorSet.IsNull())
                resources.AtmosphereDescriptorSet = _descriptorManager.AllocateAtmosphereDescriptor();

            _descriptorManager.UpdateAtmosphereDescriptor(resources.AtmosphereDescriptorSet,
                resources.ResolveColorImageView.VkImageView, resources.EntityIdDepthImageView.VkImageView);
        }
    }

    private VkSampleCountFlags GetMaxSampleCount()
    {
        var properties = _physicalDevice.GetProperties();

        var counts = properties.Limits.FramebufferColorSampleCounts &
                     properties.Limits.FramebufferDepthSampleCounts;

        if ((Int32)(counts & VkSampleCountFlags._64Bit) != 0) return VkSampleCountFlags._64Bit;
        if ((Int32)(counts & VkSampleCountFlags._32Bit) != 0) return VkSampleCountFlags._32Bit;
        if ((Int32)(counts & VkSampleCountFlags._16Bit) != 0) return VkSampleCountFlags._16Bit;
        if ((Int32)(counts & VkSampleCountFlags._8Bit) != 0) return VkSampleCountFlags._8Bit;
        if ((Int32)(counts & VkSampleCountFlags._4Bit) != 0) return VkSampleCountFlags._4Bit;
        if ((Int32)(counts & VkSampleCountFlags._2Bit) != 0) return VkSampleCountFlags._2Bit;

        return VkSampleCountFlags._1Bit;
    }

    public void DrawFrame(ReadOnlySpan<RenderObject> renderObjects, GpuCamera camera,
        ReadOnlySpan<GpuLight> lights)
    {
        if (!BeginFrame(out var imageIndex, out var isSuboptimal)) return;
        RecordFrame(renderObjects, camera, imageIndex, lights);
        SubmitFrame(imageIndex);
        PresentFrame(imageIndex, isSuboptimal);

        _frameIndex = (_frameIndex + 1) % Constants.MaxFramesInFlight;
    }

    private Boolean BeginFrame(out Int32 imageIndex, out Boolean isSuboptimal)
    {
        _device.WaitForFence(_frameResources[_frameIndex].Fence, -1);

        _picker.Collect(_frameIndex);

        var result = _device.AcquireNextImageKHR(_swapchain, -1, _frameResources[_frameIndex].ImageAvailable,
            VkFence.NullHandle,
            out imageIndex
        );

        if (result == VkResult.ErrorOutOfDateKHR)
        {
            RecreateSwapchain();
            isSuboptimal = false;
            return false;
        }

        isSuboptimal = (result == VkResult.SuboptimalKHR);

        if (result is not (VkResult.Success or VkResult.SuboptimalKHR))
            throw new InvalidOperationException($"Failed to acquire swapchain image: {result}");

        _frameResources[_frameIndex].CommandBuffer.Reset();

        return true;
    }

    private void RecordFrame(ReadOnlySpan<RenderObject> renderObjects, GpuCamera camera, Int32 imageIndex,
        ReadOnlySpan<GpuLight> lights)
    {
        var resources = _frameResources[_frameIndex];

        var context = new RenderContext
        {
            FrameResources = resources,
            SwapchainImageView = _swapchainImageViews[imageIndex],
            Extent = Extent,
            Camera = camera,
            GlobalDescriptorSet = _descriptorManager.GlobalSet,
            FrameIndex = _frameIndex
        };

        resources.CommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

        ImageUtility.TransitionImage(
            _swapchainImages[imageIndex],
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            resources.CommandBuffer
        );

        _imGuiManager.RenderFrame(_frameIndex);

        _renderItems.Clear();
        foreach (var o in renderObjects)
        {
            _renderItems.Add(new RenderItem
                {
                    Pipeline = _pipelineManager.GetPipeline(o.PipelineId),
                    GpuMesh = _meshManager.GetGpuMesh(o.MeshId),
                    ModelMatrix = o.ModelMatrix,
                    TextureId = o.TextureId,
                    Color = o.Color,
                    DataType = o.DataType,
                    IsSelected = o.IsSelected,
                    Entity = o.Entity
                }
            );
        }

        var items = _renderItems.AsSpan();

        UpdateLights(lights);
        UpdateCamera(camera);

        _entityIdPass.Record(context, items);
        _picker.Record(resources.CommandBuffer, _frameIndex, resources.EntityIdImage, Extent);

        _selectionMaskPass.Record(context, items);

        _opaquePass.Record(context, items);

        _atmospherePass.Record(context, items);

        _seedInitPass.Record(context, items);
        _jumpFloodPass.Record(context, items);
        _outlineCompositePass.Record(context, items);

        _imGuiPass.Record(context, items);

        ImageUtility.TransitionImage(
            _swapchainImages[imageIndex],
            VkImageLayout.ColorAttachmentOptimal,
            VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkPipelineStageFlags.BottomOfPipeBit,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkAccessFlags.None,
            VkImageAspectFlags.ColorBit,
            resources.CommandBuffer
        );

        resources.CommandBuffer.End();
    }

    private void SubmitFrame(Int32 imageIndex)
    {
        var resources = _frameResources[_frameIndex];

        _device.ResetFences([resources.Fence]);

        _queue.Submit(
            [resources.ImageAvailable],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [resources.CommandBuffer],
            [_renderFinished[imageIndex]],
            resources.Fence
        );
    }

    private unsafe void PresentFrame(Int32 imageIndex, Boolean isSuboptimal)
    {
        var swapchain = _swapchain;
        var signalSemaphore = _renderFinished[imageIndex];

        var result = _queue.PresentKHR(
            new VkPresentInfoKHR
            {
                WaitSemaphoreCount = 1,
                WaitSemaphores = &signalSemaphore,
                SwapchainCount = 1,
                Swapchains = &swapchain,
                ImageIndices = &imageIndex
            }
        );

        if (result is not (VkResult.Success or VkResult.SuboptimalKHR or VkResult.ErrorOutOfDateKHR))
            throw new InvalidOperationException($"Failed to present swapchain image: {result}");

        if (_isFramebufferResized || isSuboptimal || result is VkResult.ErrorOutOfDateKHR or VkResult.SuboptimalKHR)
        {
            _isFramebufferResized = false;
            RecreateSwapchain();
        }
    }

    public void OnFramebufferResize()
    {
        _isFramebufferResized = true;
    }

    public void Dispose()
    {
        foreach (var resource in _frameResources)
        {
            _device.DestroyFence(resource.Fence, null);
            _device.DestroySemaphore(resource.ImageAvailable, null);
        }

        _lightsBuffer.Dispose();
        _cameraBuffer.Dispose();
        _picker.Dispose();

        CleanupSwapchain();
    }
}