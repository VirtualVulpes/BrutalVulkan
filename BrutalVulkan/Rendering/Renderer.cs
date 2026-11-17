using Brutal;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.ECS.Systems;
using BrutalVulkan.Rendering.GpuTypes;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Passes;
using BrutalVulkan.Rendering.Passes.Compute;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.UI;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Rendering;

public sealed class Renderer : IDisposable
{
    private readonly DeviceEx _device;
    private readonly PhysicalDevice _physicalDevice;
    private readonly VkSurfaceKHR _surface;
    private readonly GlfwWindow _window;
    private readonly VmaAllocator _allocator;
    private readonly Queue _queue;

    private DescriptorManager _descriptorManager;
    private ImGuiManager _imGuiManager;
    private MeshManager _meshManager;
    private PipelineManager _pipelineManager;
    private List<IRenderPass> _computePasses;
    private List<IRenderPass> _renderPasses;

    private readonly FrameResources[] _frameResources = new FrameResources[Constants.MaxFramesInFlight];

    private VkSemaphore[] _renderFinished;

    private VkSwapchainKHR _swapchain;
    private VkImage[] _swapchainImages;
    private VkImageView[] _swapchainImageViews;

    private Boolean _isFramebufferResized;
    private Int32 _frameIndex;

    private RenderItem[] _renderObjects = [];

    private BufferEx _lightsBuffer;

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
        _queue = queue;

        MsaaSamples = (VkSampleCountFlags)Int32.Min(Constants.DefaultMsaa, (Int32)GetMaxSampleCount());

        for (var i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _frameResources[i].ImageAvailable = _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
            _frameResources[i].Fence = _device.CreateFence(VkFenceCreateFlags.SignaledBit, null);
            _frameResources[i].CommandBuffer = _device.AllocateCommandBuffer(
                new VkCommandBufferAllocateInfo
                {
                    CommandPool = commandPool,
                    Level = VkCommandBufferLevel.Primary
                }
            );
        }

        CreateSwapchain();
        CreateColorResources();
        CreateDepthResources();
    }

    public void Initialize(PipelineManager pipelineManager, DescriptorManager descriptorManager,
        ImGuiManager imGuiManager,
        MeshManager meshManager)
    {
        _descriptorManager = descriptorManager;
        _imGuiManager = imGuiManager;
        _meshManager = meshManager;
        _pipelineManager = pipelineManager;

        _computePasses =
        [
            new TestComputePass(_pipelineManager.GetPipeline("Test Compute"))
        ];

        _renderPasses =
        [
            new SelectionMaskPass(_pipelineManager.GetPipeline("Selection Mask")),
            new ImageDisplayPass(_pipelineManager.GetPipeline("Fullscreen Triangle")),
            new OpaquePass(),
            new ImGuiPass(_imGuiManager)
        ];

        CreateLightBuffer();
        UpdateLightDescriptor();
        CreateSelectionMaskResources();
        CreatePostProcessResources();
    }

    private void CreateLightBuffer()
    {
        var bufferInfo = new BufferEx.CreateInfo
        {
            Name = "Lights Storage Buffer",
            BufferSize = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights,
            BufferUsage = VkBufferUsageFlags.StorageBufferBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };
        _lightsBuffer = _device.CreateBuffer(bufferInfo);
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

    private unsafe void UpdateLightDescriptor()
    {
        var bufferInfo = new VkDescriptorBufferInfo
        {
            Buffer = _lightsBuffer.VkBuffer,
            Offset = ByteSize.Zero,
            Range = ByteSize.Of<UInt32>() + ByteSize.Of<GpuLight>() * Constants.MaxLights
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

        _swapchainImages = new VkImage[_device.GetSwapchainImagesKHR_Count(_swapchain)];
        _device.GetSwapchainImagesKHR_Write(_swapchain, _swapchainImages);

        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        CreateSwapchainImageViews();

        _renderFinished = [.. _swapchainImages.Select(_ => _device.CreateSemaphore(new VkSemaphoreCreateInfo(), null))];
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

            resource.SelectionMaskImageView.Dispose();
            resource.SelectionMaskImage.Dispose();
            
            resource.SelectionMaskDepthImageView.Dispose();
            resource.SelectionMaskDepthImage.Dispose();
            
            resource.ComputeImageView.Dispose();
            resource.ComputeImage.Dispose();
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
        CreateColorResources();
        CreateDepthResources();
        CreateSelectionMaskResources();
        CreatePostProcessResources();

        _imGuiManager.UpdateDisplaySize();
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

        var colorSubresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            LayerCount = 1,
            LevelCount = 1
        };

        for (var i = 0; i < _frameResources.Length; i++)
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
            _frameResources[i].DepthImage = depthImage;
            _frameResources[i].DepthImageView = depthImage.CreateImageView(
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

            var image = _allocator.CreateImage(createInfo);

            resources.SelectionMaskImage = image;
            resources.SelectionMaskImageView = image.CreateImageView(
                VkImageViewType._2D, subresourceRange, image.Allocator.VkAllocator);
        }
        
        var depthInfo = new ImageEx.CreateInfo
        {
            Name = "Selection Mask Depth Image",
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
            _frameResources[i].SelectionMaskDepthImage = depthImage;
            _frameResources[i].SelectionMaskDepthImageView= depthImage.CreateImageView(
                VkImageViewType._2D, depthSubresourceRange, depthImage.Allocator.VkAllocator);
        }
    }

    private void CreatePostProcessResources()
    {
        var createInfo = new ImageEx.CreateInfo
        {
            Name = "Test Compute Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.R8G8B8A8UNorm,
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

            var computeImage = _allocator.CreateImage(createInfo);

            resources.ComputeImage = computeImage;
            resources.ComputeImageView = computeImage.CreateImageView(
                VkImageViewType._2D, subresourceRange, computeImage.Allocator.VkAllocator);

            if (resources.ComputeDescriptorSet.IsNull())
                resources.ComputeDescriptorSet = _descriptorManager.AllocateComputeDescriptorSet();

            if (resources.DisplayDescriptorSet.IsNull())
                resources.DisplayDescriptorSet = _descriptorManager.AllocateDisplayDescriptorSet();

            _descriptorManager.UpdateComputeDescriptor(resources.ComputeDescriptorSet,
                resources.ComputeImageView.VkImageView);
            _descriptorManager.UpdateDisplayDescriptor(resources.DisplayDescriptorSet,
                resources.ComputeImageView.VkImageView);
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

    public void DrawFrame(ReadOnlySpan<RenderObject> renderObjects, CameraData cameraData,
        ReadOnlySpan<GpuLight> lights)
    {
        if (!BeginFrame(out var imageIndex, out var isSuboptimal)) return;
        RecordFrame(renderObjects, cameraData, imageIndex, lights);
        SubmitFrame(imageIndex);
        PresentFrame(imageIndex, isSuboptimal);

        _frameIndex = (_frameIndex + 1) % Constants.MaxFramesInFlight;
    }

    private Boolean BeginFrame(out Int32 imageIndex, out Boolean isSuboptimal)
    {
        _device.WaitForFence(_frameResources[_frameIndex].Fence, -1);

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

    private unsafe void RecordFrame(ReadOnlySpan<RenderObject> renderObjects, CameraData cameraData, Int32 imageIndex,
        ReadOnlySpan<GpuLight> lights)
    {
        var resources = _frameResources[_frameIndex];

        var context = new RenderContext
        {
            FrameResources = resources,
            ResolveImageView = _swapchainImageViews[imageIndex],
            Extent = Extent,
            ViewProj = cameraData.View * cameraData.Projection,
            CameraPosition = cameraData.Position,
            GlobalDescriptorSet = _descriptorManager.GlobalSet,
            FrameIndex = _frameIndex
        };

        resources.CommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
        
        ImageUtility.TransitionImage(
            resources.ComputeImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.General,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.ComputeShaderBit,
            VkAccessFlags.None,
            VkAccessFlags.ShaderWriteBit,
            VkImageAspectFlags.ColorBit,
            resources.CommandBuffer
        );

        foreach (var pass in _computePasses)
        {
            pass.Record(context, []);
        }

        ImageUtility.TransitionImage(
            resources.ComputeImage.VkImage,
            VkImageLayout.General,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags.ComputeShaderBit,
            VkPipelineStageFlags.FragmentShaderBit,
            VkAccessFlags.ShaderWriteBit,
            VkAccessFlags.ShaderReadBit,
            VkImageAspectFlags.ColorBit,
            resources.CommandBuffer
        );

        _imGuiManager.RenderFrame(_frameIndex);

        ImageUtility.TransitionImage(
            _swapchainImages[imageIndex],
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            resources.CommandBuffer
        );
        
        ImageUtility.TransitionImage(
            context.FrameResources.ColorImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            context.CommandBuffer
        );

        ImageUtility.TransitionImage(
            context.FrameResources.DepthImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.DepthAttachmentOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.EarlyFragmentTestsBit | VkPipelineStageFlags.LateFragmentTestsBit,
            VkAccessFlags.None,
            VkAccessFlags.DepthStencilAttachmentWriteBit,
            VkImageAspectFlags.DepthBit,
            context.CommandBuffer
        );
        
        if (_renderObjects.Length < renderObjects.Length)
            Array.Resize(ref _renderObjects, renderObjects.Length);

        for (var i = 0; i < renderObjects.Length; i++)
        {
            _renderObjects[i] = new RenderItem
            {
                Pipeline = _pipelineManager.GetPipeline(renderObjects[i].PipelineId),
                GpuMesh = _meshManager.GetGpuMesh(renderObjects[i].MeshId),
                ModelMatrix = renderObjects[i].ModelMatrix,
                TextureId = renderObjects[i].TextureId,
                Color = renderObjects[i].Color,
                DataType = renderObjects[i].DataType,
                IsSelected = renderObjects[i].IsSelected
            };
        }

        UpdateLights(lights);

        foreach (var pass in _renderPasses)
        {
            pass.Record(context, _renderObjects);
        }
        
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

        CleanupSwapchain();
    }
}