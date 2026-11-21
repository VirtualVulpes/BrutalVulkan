using Brutal;
using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
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
    private static readonly VkSampleCountFlags[] SampleCounts =
    [
        VkSampleCountFlags._64Bit, VkSampleCountFlags._32Bit,
        VkSampleCountFlags._16Bit, VkSampleCountFlags._8Bit, VkSampleCountFlags._4Bit,
        VkSampleCountFlags._2Bit
    ];

    private readonly VulkanContext _context;
    private readonly GlfwWindow _window;

    private DescriptorManager _descriptorManager;
    private ImGuiManager _imGuiManager;
    private MeshManager _meshManager;
    private PipelineManager _pipelineManager;

    private readonly FrameResources[] _frameResources = new FrameResources[Constants.MaxFramesInFlight];

    public Swapchain Swapchain { get; }

    private Boolean _isFramebufferResized;
    private Int32 _frameIndex;

    private readonly List<RenderItem> _renderItems = [];

    private IRenderPass[] _renderPasses;

    private EntityPicker _picker;

    public VkSampleCountFlags MsaaSamples { get; }

    public Renderer(VulkanContext context, GlfwWindow window)
    {
        _context = context;
        _window = window;

        MsaaSamples = (VkSampleCountFlags)Int32.Min(Constants.DefaultMsaa, (Int32)GetMaxSampleCount());

        for (var i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _frameResources[i].ImageAvailable = _context.Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
            _frameResources[i].Fence = _context.Device.CreateFence(VkFenceCreateFlags.SignaledBit, null);
            _frameResources[i].CommandBuffer = _context.Device.AllocateCommandBuffer(
                new VkCommandBufferAllocateInfo
                {
                    CommandPool = _context.CommandPool,
                    Level = VkCommandBufferLevel.Primary
                }
            );
        }

        Swapchain = new Swapchain(_context, _window);
    }

    public void Initialize(PipelineManager pipelineManager, DescriptorManager descriptorManager,
        ImGuiManager imGuiManager,
        MeshManager meshManager, EntityPicker picker)
    {
        _descriptorManager = descriptorManager;
        _imGuiManager = imGuiManager;
        _meshManager = meshManager;
        _pipelineManager = pipelineManager;

        _picker = picker;

        _renderPasses =
        [
            new EntityIdPass(_pipelineManager.GetPipeline("Entity Id")),
            new SelectionMaskPass(_pipelineManager.GetPipeline("Selection Mask")),
            new OpaquePass(),
            new AtmospherePass(_pipelineManager.GetPipeline("Atmosphere")),
            new SeedInitPass(_pipelineManager.GetPipeline("Seed Init")),
            new JumpFloodPass(_pipelineManager.GetPipeline("Jump Flood")),
            new OutlineCompositePass(_pipelineManager.GetPipeline("Outline Composite")),
            new ImGuiPass(_imGuiManager)
        ];

        AllocateDescriptorSets();

        CreateFrameBuffers();
        UpdateFrameDescriptors();

        CreateResources();
    }

    private void CreateFrameBuffers()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var r = ref _frameResources[i];

            var lightBuffer = new BufferEx.CreateInfo
            {
                Name = $"Lights Storage Buffer {i}",
                BufferSize = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights,
                BufferUsage = VkBufferUsageFlags.StorageBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.LightsBuffer = _context.Device.CreateBuffer(lightBuffer);
            r.LightsMemory = r.LightsBuffer.Map();

            var cameraBuffer = new BufferEx.CreateInfo
            {
                Name = $"Camera Buffer {i}",
                BufferSize = ByteSize.Of<GpuCamera>(),
                BufferUsage = VkBufferUsageFlags.UniformBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.CameraBuffer = _context.Device.CreateBuffer(cameraBuffer);
            r.CameraMemory = r.CameraBuffer.Map();
        }
    }

    private unsafe void UpdateLights(ref FrameResources r, ReadOnlySpan<GpuLight> lights)
    {
        if (lights.Length > Constants.MaxLights)
            throw new ArgumentOutOfRangeException(nameof(lights));

        var count = lights.Length;

        Buffer.MemoryCopy(
            &count,
            r.LightsMemory.MapPtr.Pointer,
            ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights,
            ByteSize.Of<UInt32>()
        );

        fixed (GpuLight* lightsPtr = lights)
        {
            Buffer.MemoryCopy(
                lightsPtr,
                (Byte*)r.LightsMemory.MapPtr.Pointer + ByteSize.Of<UInt32>() * 4,
                ByteSize.Of<GpuLight>() * Constants.MaxLights,
                ByteSize.Of<GpuLight>() * lights.Length
            );
        }
    }

    private unsafe void UpdateCamera(ref FrameResources r, GpuCamera camera)
    {
        Buffer.MemoryCopy(
            &camera,
            r.CameraMemory.MapPtr.Pointer,
            ByteSize.Of<GpuCamera>(),
            ByteSize.Of<GpuCamera>()
        );
    }

    private unsafe void UpdateFrameDescriptors()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var r = ref _frameResources[i];

            var lightsInfo = new VkDescriptorBufferInfo
            {
                Buffer = r.LightsBuffer.VkBuffer,
                Offset = ByteSize.Zero,
                Range = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * Constants.MaxLights
            };

            var cameraInfo = new VkDescriptorBufferInfo
            {
                Buffer = r.CameraBuffer.VkBuffer,
                Offset = ByteSize.Zero,
                Range = ByteSize.Of<GpuCamera>()
            };

            ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
            [
                new()
                {
                    DstSet = r.PerFrameDescriptorSet,
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.StorageBuffer,
                    BufferInfo = &lightsInfo
                },
                new()
                {
                    DstSet = r.PerFrameDescriptorSet,
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.UniformBuffer,
                    BufferInfo = &cameraInfo
                }
            ];

            _context.Device.UpdateDescriptorSets(descriptorWrites, null);
        }
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

        _context.Device.WaitIdle();

        Swapchain.Recreate(_window);

        foreach (ref var r in _frameResources.AsSpan())
        {
            r.DisposeTargets();
        }

        CreateResources();

        _imGuiManager.UpdateDisplaySize();
        _picker.Reset();
    }

    private void CreateResources()
    {
        CreateRenderTargets();
        UpdateImageDescriptors();
    }

    private void CreateRenderTargets()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var r = ref _frameResources[i];

            (r.ColorImage, r.ColorImageView) = CreateImage("Color Image",
                Swapchain.SurfaceFormat.Format,
                VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.TransientAttachmentBit,
                samples: MsaaSamples);

            (r.ResolveColorImage, r.ResolveColorImageView) = CreateImage("Resolve Color Image",
                Swapchain.SurfaceFormat.Format, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);

            (r.DepthImage, r.DepthImageView) = CreateImage("Depth Image",
                VkFormat.D32SFloat, VkImageUsageFlags.DepthStencilAttachmentBit, VkImageAspectFlags.DepthBit,
                MsaaSamples);

            (r.PostProcessImage, r.PostProcessImageView) = CreateImage("Post Process Image",
                Swapchain.SurfaceFormat.Format, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit);

            (r.EntityIdImage, r.EntityIdImageView) = CreateImage("Entity Id Image",
                VkFormat.R32UInt,
                VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit |
                VkImageUsageFlags.ColorAttachmentBit);

            (r.EntityIdDepthImage, r.EntityIdDepthImageView) = CreateImage("Entity Id Depth Image",
                VkFormat.D32SFloat, VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.SampledBit,
                VkImageAspectFlags.DepthBit);

            (r.SelectionMaskImage, r.SelectionMaskImageView) = CreateImage("Selection Mask Image",
                VkFormat.R8UNorm,
                VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit |
                VkImageUsageFlags.ColorAttachmentBit);

            (r.SeedAImage, r.SeedAImageView) = CreateImage("Seed A Image",
                VkFormat.R32G32SInt,
                VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);

            (r.SeedBImage, r.SeedBImageView) = CreateImage("Seed B Image",
                VkFormat.R32G32SInt,
                VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.StorageBit | VkImageUsageFlags.SampledBit);
        }
    }

    private void AllocateDescriptorSets()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var r = ref _frameResources[i];

            r.PerFrameDescriptorSet = _descriptorManager.AllocatePerFrameDescriptorSet();
            r.SeedDescriptorSet = _descriptorManager.AllocateSeedDescriptorSet();
            r.SeedAToBDescriptorSet = _descriptorManager.AllocateSeedAToBDescriptorSet();
            r.SeedBToADescriptorSet = _descriptorManager.AllocateSeedBToADescriptorSet();
            r.OutlineCompositeDescriptorSet = _descriptorManager.AllocateOutlineDescriptorSet();
            r.AtmosphereDescriptorSet = _descriptorManager.AllocateAtmosphereDescriptor();
        }
    }

    private void UpdateImageDescriptors()
    {
        for (var i = 0; i < _frameResources.Length; i++)
        {
            ref var r = ref _frameResources[i];

            _descriptorManager.UpdateSeedDescriptor(r.SeedDescriptorSet,
                r.SelectionMaskImageView.VkImageView, r.SeedAImageView.VkImageView);
            _descriptorManager.UpdateSeedDescriptor(r.SeedAToBDescriptorSet,
                r.SeedAImageView.VkImageView, r.SeedBImageView.VkImageView);
            _descriptorManager.UpdateSeedDescriptor(r.SeedBToADescriptorSet,
                r.SeedBImageView.VkImageView, r.SeedAImageView.VkImageView);
            _descriptorManager.UpdateOutlineDescriptor(r.OutlineCompositeDescriptorSet,
                r.PostProcessImageView.VkImageView, r.SeedAImageView.VkImageView);
            _descriptorManager.UpdateAtmosphereDescriptor(r.AtmosphereDescriptorSet,
                r.ResolveColorImageView.VkImageView, r.EntityIdDepthImageView.VkImageView);
        }
    }

    private (TrackedImage Image, ImageViewEx View) CreateImage(String name, VkFormat format, VkImageUsageFlags usage,
        VkImageAspectFlags aspect = VkImageAspectFlags.ColorBit, VkSampleCountFlags samples = VkSampleCountFlags._1Bit)
    {
        var image = _context.Allocator.CreateImage(new ImageEx.CreateInfo
        {
            Name = name,
            ImageType = VkImageType._2D,
            ImageFormat = format,
            ImageExtent = new VkExtent3D
            {
                Width = Swapchain.Extent.Width,
                Height = Swapchain.Extent.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = samples,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = usage,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        });

        var view = image.CreateImageView(VkImageViewType._2D, new VkImageSubresourceRange
        {
            AspectMask = aspect,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        }, null);

        return (new TrackedImage(image), view);
    }

    private VkSampleCountFlags GetMaxSampleCount()
    {
        var p = _context.PhysicalDevice.GetProperties();
        var counts = p.Limits.FramebufferColorSampleCounts & p.Limits.FramebufferDepthSampleCounts;

        foreach (var c in SampleCounts)
        {
            if ((Int32)(counts & c) != 0) return c;
        }

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
        _context.Device.WaitForFence(_frameResources[_frameIndex].Fence, -1);

        _picker.Collect(_frameIndex);

        var result = _context.Device.AcquireNextImageKHR(Swapchain.Handle, -1,
            _frameResources[_frameIndex].ImageAvailable,
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
        ref var r = ref _frameResources[_frameIndex];

        var context = new RenderContext
        {
            FrameResources = r,
            SwapchainImageView = Swapchain.ImageView(imageIndex),
            Extent = Swapchain.Extent,
            Camera = camera,
            GlobalDescriptorSet = _descriptorManager.GlobalSet,
            FrameIndex = _frameIndex
        };

        r.CommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);

        ImageUtility.TransitionImage(
            Swapchain.Image(imageIndex),
            VkImageLayout.Undefined,
            VkImageLayout.ColorAttachmentOptimal,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkAccessFlags.None,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkImageAspectFlags.ColorBit,
            r.CommandBuffer
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

        UpdateLights(ref r, lights);
        UpdateCamera(ref r, camera);

        foreach (var pass in _renderPasses)
        {
            pass.Record(context, items);
        }

        _picker.Record(r.CommandBuffer, _frameIndex, r.EntityIdImage, Swapchain.Extent);

        ImageUtility.TransitionImage(
            Swapchain.Image(imageIndex),
            VkImageLayout.ColorAttachmentOptimal,
            VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags.ColorAttachmentOutputBit,
            VkPipelineStageFlags.BottomOfPipeBit,
            VkAccessFlags.ColorAttachmentWriteBit,
            VkAccessFlags.None,
            VkImageAspectFlags.ColorBit,
            r.CommandBuffer
        );

        r.CommandBuffer.End();
    }

    private void SubmitFrame(Int32 imageIndex)
    {
        ref var resources = ref _frameResources[_frameIndex];

        _context.Device.ResetFences([resources.Fence]);

        _context.Queue.Submit(
            [resources.ImageAvailable],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [resources.CommandBuffer],
            [Swapchain.RenderFinished(imageIndex)],
            resources.Fence
        );
    }

    private unsafe void PresentFrame(Int32 imageIndex, Boolean isSuboptimal)
    {
        var swapchain = Swapchain.Handle;
        var signalSemaphore = Swapchain.RenderFinished(imageIndex);

        var result = _context.Queue.PresentKHR(
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
        foreach (ref var r in _frameResources.AsSpan())
        {
            r.DisposeTargets();
            r.DestroySync(_context.Device);
        }

        Swapchain.Dispose();
    }
}