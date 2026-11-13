using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan;

public readonly struct RenderObject
{
    public GpuMesh Mesh { get; init; }
    public float4x4 ModelMatrix { get; init; }
    public Material Material { get; init; }
}

public sealed class Renderer : IDisposable
{
    private struct FrameResources
    {
        public CommandBuffer CommandBuffer;
        public VkFence Fence;
        public VkSemaphore ImageAvailable;

        public ImageEx ColorImage;
        public ImageViewEx ColorImageView;

        public ImageEx DepthImage;
        public ImageViewEx DepthImageView;
    }

    private readonly DeviceEx _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly VkSurfaceKHR _surface;
    private readonly GlfwWindow _window;
    private readonly VmaAllocator _allocator;
    private readonly VkCommandPool _commandPool;
    private readonly Queue _queue;
    private PipelineManager _pipelineManager;
    private VkDescriptorSet[] _descriptorSets;

    private VkSemaphore[] _renderFinished;
    private FrameResources[] _frameResources = new FrameResources[Constants.MaxFramesInFlight];

    private VkImage[] _swapchainImages;
    private VkImageView[] _swapchainImageViews;

    private VkSwapchainKHR _swapchain;
    public VkSurfaceFormatKHR SwapchainFormat { get; private set; }

    private VkExtent2D _extent;

    private bool _isFramebufferResized = false;

    private uint _frameIndex = 0;

    public VkSampleCountFlags MsaaSamples { get; }

    private List<IRenderPass> _renderPasses;

    public Renderer(DeviceEx device, PhysicalDevice physicalDevice, VkSurfaceKHR surface,
        GlfwWindow window, VmaAllocator allocator, VkCommandPool commandPool, Queue queue,
        PipelineManager pipelineManager, VkDescriptorSet[] descriptorSets)
    {
        _device = device;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _window = window;
        _allocator = allocator;
        _commandPool = commandPool;
        _queue = queue;
        _pipelineManager = pipelineManager;
        _descriptorSets = descriptorSets;

        _renderPasses = [new OpaquePass(_pipelineManager)];

        MsaaSamples = (VkSampleCountFlags)int.Min(Constants.DefaultMsaa, (int)GetMaxSampleCount());

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

        CreateColorResources();
        CreateDepthResources();
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
        
        SwapchainFormat = ChooseSurfaceFormat();

        _swapchain = _device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = SwapchainFormat.Format,
                ImageColorSpace = SwapchainFormat.ColorSpace,
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

        _swapchainImages = new VkImage[_device.GetSwapchainImagesKHR_Count(_swapchain)];
        _device.GetSwapchainImagesKHR_Write(_swapchain, _swapchainImages);

        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        CreateSwapchainImageViews();

        _renderFinished = _swapchainImages.Select(_ => _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null))
            .ToArray();
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
            if (format is { Format: VkFormat.B8G8R8SRGB, ColorSpace: VkColorSpaceKHR.SRGBNonlinearKHR })
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
        }

        _device.DestroySwapchainKHR(_swapchain, null);
        _swapchain = VkSwapchainKHR.NullHandle;
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

        CreateColorResources();
        CreateDepthResources();
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
                Width = _extent.Width,
                Height = _extent.Height,
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

        var colorSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LayerCount = 1,
            LevelCount = 1
        };

        for (int i = 0; i < _frameResources.Length; i++)
        {
            var colorImage = _allocator.CreateImage(colorInfo);
            _frameResources[i].ColorImage = colorImage;
            _frameResources[i].ColorImageView = colorImage.CreateImageView(
                VkImageViewType._2D, colorSubresourceRange, colorImage.Allocator.VkAllocator);
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
                Width = _extent.Width,
                Height = _extent.Height,
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

        for (int i = 0; i < _frameResources.Length; i++)
        {
            var depthImage = _allocator.CreateImage(depthInfo);
            _frameResources[i].DepthImage = depthImage;
            _frameResources[i].DepthImageView = depthImage.CreateImageView(
                VkImageViewType._2D, depthSubresourceRange, depthImage.Allocator.VkAllocator);
        }
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

    public void DrawFrame(ReadOnlySpan<RenderObject> renderObjects, float4x4 viewProj)
    {
        BeginFrame(out var imageIndex);
        RecordFrame(renderObjects, viewProj, imageIndex);
        SubmitFrame(imageIndex);
        PresentFrame(imageIndex);

        _frameIndex = (_frameIndex + 1) % Constants.MaxFramesInFlight;
    }

    private void BeginFrame(out int imageIndex)
    {
        _device.WaitForFence(_frameResources[_frameIndex].Fence, -1);

        var result = _device.AcquireNextImageKHR(_swapchain, -1, _frameResources[_frameIndex].ImageAvailable,
            VkFence.NullHandle,
            out imageIndex);

        if (result == VkResult.ErrorOutOfDateKHR)
        {
            RecreateSwapchain();
            return;
        }

        _device.ResetFences([_frameResources[_frameIndex].Fence]);

        _frameResources[_frameIndex].CommandBuffer.Reset();
    }

    private void RecordFrame(ReadOnlySpan<RenderObject> renderObjects, float4x4 viewProj, int imageIndex)
    {
        var resources = _frameResources[_frameIndex];

        var context = new RenderContext
        {
            CommandBuffer = resources.CommandBuffer,
            ColorImage = resources.ColorImage,
            ColorImageView = resources.ColorImageView,
            DepthImage = resources.DepthImage,
            DepthImageView = resources.DepthImageView,
            ResolveImageView = _swapchainImageViews[imageIndex],
            Extent = _extent,
            ViewProj = viewProj,
            GlobalDescriptorSets = _descriptorSets
        };

        resources.CommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

        App.TransitionImage(
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

        foreach (var pass in _renderPasses)
        {
            pass.Record(context, renderObjects);
        }

        App.TransitionImage(
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

    private void SubmitFrame(int imageIndex)
    {
        _queue.Submit(
            [_frameResources[_frameIndex].ImageAvailable],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [_frameResources[_frameIndex].CommandBuffer],
            [_renderFinished[imageIndex]],
            _frameResources[_frameIndex].Fence
        );
    }

    private unsafe void PresentFrame(int imageIndex)
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

        if (_isFramebufferResized || result is VkResult.ErrorOutOfDateKHR or VkResult.SuboptimalKHR)
        {
            _isFramebufferResized = false;
            RecreateSwapchain();
        }
    }

    public void SetFramebufferResized()
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

        CleanupSwapchain();
    }
}