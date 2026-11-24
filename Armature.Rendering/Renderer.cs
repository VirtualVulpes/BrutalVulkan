using Armature.Core;
using Armature.Rendering.Passes;
using Armature.Vulkan;
using Brutal;
using Brutal.GlfwApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using CommunityToolkit.HighPerformance;

namespace Armature.Rendering;

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

    private CoreDescriptors _coreDescriptors;
    private MeshManager _meshManager;
    private PipelineManager _pipelineManager;

    private readonly FrameResources[] _frameResources = new FrameResources[RenderConstants.MaxFramesInFlight];
    public RenderTargets Targets { get; private set; }

    public Swapchain Swapchain { get; }

    private Boolean _isFramebufferResized;
    private Int32 _frameIndex;

    private readonly List<RenderItem> _renderItems = [];

    private readonly List<(RenderPhase Phase, IRenderPass Pass)> _passes = [];
    private readonly List<IFrameListener> _listeners = [];

    public VkSampleCountFlags MsaaSamples { get; }

    public Renderer(VulkanContext context, GlfwWindow window)
    {
        _context = context;
        _window = window;

        MsaaSamples = (VkSampleCountFlags)Int32.Min(RenderConstants.DefaultMsaa, (Int32)GetMaxSampleCount());

        for (var i = 0; i < RenderConstants.MaxFramesInFlight; i++)
        {
            _frameResources[i] = new FrameResources
            {
                ImageAvailable = _context.Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null),
                Fence = _context.Device.CreateFence(VkFenceCreateFlags.SignaledBit, null),
                CommandBuffer = _context.Device.AllocateCommandBuffer(
                    new VkCommandBufferAllocateInfo
                    {
                        CommandPool = _context.CommandPool,
                        Level = VkCommandBufferLevel.Primary
                    }
                )
            };
        }

        Swapchain = new Swapchain(_context, _window);
        Targets = new RenderTargets(context.Allocator, Swapchain.SurfaceFormat.Format, Swapchain.Extent, MsaaSamples);
    }

    public void Initialize(PipelineManager pipelineManager, CoreDescriptors coreDescriptors,
        MeshManager meshManager)
    {
        _coreDescriptors = coreDescriptors;
        _meshManager = meshManager;
        _pipelineManager = pipelineManager;

        AllocateDescriptorSets();

        CreatePerFrameBuffers();
        UpdateFrameDescriptors();
    }

    public void AddPass(IRenderPass pass, RenderPhase phase)
    {
        var i = _passes.FindLastIndex(p => p.Phase <= phase) + 1;
        _passes.Insert(i, (phase, pass));
        if (pass is ITargetConsumer c) c.OnTargetsRecreated(Targets);
    }

    public void AddListener(IFrameListener l) => _listeners.Add(l);

    private void CreatePerFrameBuffers()
    {
        foreach (var r in _frameResources)
        {
            var lightBuffer = new BufferEx.CreateInfo
            {
                Name = "Lights Storage Buffer",
                BufferSize = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
                BufferUsage = VkBufferUsageFlags.StorageBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.LightsBuffer = _context.Device.CreateBuffer(lightBuffer);
            r.LightsMemory = r.LightsBuffer.Map();

            var cameraBuffer = new BufferEx.CreateInfo
            {
                Name = "Camera Buffer",
                BufferSize = ByteSize.Of<GpuCamera>(),
                BufferUsage = VkBufferUsageFlags.UniformBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.CameraBuffer = _context.Device.CreateBuffer(cameraBuffer);
            r.CameraMemory = r.CameraBuffer.Map();
        }
    }

    private unsafe void UpdateLights(FrameResources r, ReadOnlySpan<GpuLight> lights)
    {
        if (lights.Length > RenderConstants.MaxLights)
            throw new ArgumentOutOfRangeException(nameof(lights));

        var count = lights.Length;

        Buffer.MemoryCopy(
            &count,
            r.LightsMemory.MapPtr.Pointer,
            ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
            ByteSize.Of<UInt32>()
        );

        fixed (GpuLight* lightsPtr = lights)
        {
            Buffer.MemoryCopy(
                lightsPtr,
                (Byte*)r.LightsMemory.MapPtr.Pointer + ByteSize.Of<UInt32>() * 4,
                ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
                ByteSize.Of<GpuLight>() * lights.Length
            );
        }
    }

    private unsafe void UpdateCamera(FrameResources r, GpuCamera camera)
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
        foreach (var r in _frameResources)
        {
            var lightsInfo = new VkDescriptorBufferInfo
            {
                Buffer = r.LightsBuffer.VkBuffer,
                Offset = ByteSize.Zero,
                Range = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights
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

        Targets.Dispose();
        Targets = new RenderTargets(_context.Allocator, Swapchain.SurfaceFormat.Format, Swapchain.Extent, MsaaSamples);
        NotifyTargetsRecreated();
        
        foreach (var l in _listeners) l.OnResize(Targets);
    }

    private void AllocateDescriptorSets()
    {
        foreach (var r in _frameResources)
        {
            r.PerFrameDescriptorSet = _coreDescriptors.AllocatePerFrameDescriptorSet();
        }
    }

    private void NotifyTargetsRecreated()
    {
        foreach (var (_, pass) in _passes)
            if (pass is ITargetConsumer c)
                c.OnTargetsRecreated(Targets);
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

    public void DrawFrame(SceneData scene)
    {
        if (!BeginFrame(out var imageIndex, out var isSuboptimal)) return;
        RecordFrame(imageIndex, scene);
        SubmitFrame(imageIndex);
        PresentFrame(imageIndex, isSuboptimal);

        _frameIndex = (_frameIndex + 1) % RenderConstants.MaxFramesInFlight;
    }

    private Boolean BeginFrame(out Int32 imageIndex, out Boolean isSuboptimal)
    {
        _context.Device.WaitForFence(_frameResources[_frameIndex].Fence, -1);

        foreach (var l in _listeners) l.OnFrameBegin(_frameIndex);

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

    private void RecordFrame(Int32 imageIndex, SceneData scene)
    {
        var r = _frameResources[_frameIndex];

        var context = new RenderContext
        {
            FrameResources = r,
            Targets = Targets,
            SwapchainImageView = Swapchain.ImageView(imageIndex),
            GlobalDescriptorSet = _coreDescriptors.GlobalSet,
            FrameIndex = _frameIndex,
            Scene = scene
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

        foreach (var l in _listeners) l.OnPrepare(context);

        _renderItems.Clear();
        foreach (var o in scene.ObjectSpan)
        {
            _renderItems.Add(new RenderItem
                {
                    Pipeline = _pipelineManager.GetPipeline((PipelineId)o.PipelineId),
                    GpuMesh = _meshManager.GetGpuMesh(o.MeshId),
                    ModelMatrix = o.ModelMatrix,
                    TextureId = o.TextureId,
                    Color = o.Color,
                    DataType = o.DataType,
                    Flags = o.Flags,
                    ItemId = o.ObjectId
                }
            );
        }

        var items = _renderItems.AsSpan();

        UpdateLights(r, scene.LightSpan);
        UpdateCamera(r, scene.Camera);

        
        foreach (var (_, pass) in _passes)
        {
            pass.Record(context, items);
        }

        foreach (var l in _listeners) l.OnPostRecord(context);

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
        var r = _frameResources[_frameIndex];

        _context.Device.ResetFences([r.Fence]);

        _context.Queue.Submit(
            [r.ImageAvailable],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [r.CommandBuffer],
            [Swapchain.RenderFinished(imageIndex)],
            r.Fence
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
        foreach (var r in _frameResources)
        {
            r.DestroySync(_context.Device);
        }

        Targets.Dispose();
        Swapchain.Dispose();
    }
}