using Armature.Core;
using Armature.Rendering.Passes;
using Armature.Vulkan;
using Brutal;
using Brutal.GlfwApi;
using Brutal.Numerics;
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

    private readonly FrameResources[] _frameResources = new FrameResources[RenderConstants.MaxFramesInFlight];
    public RenderTargets Targets { get; private set; }

    public Swapchain Swapchain { get; }

    private bool _isFramebufferResized;
    private int _frameIndex;
    
    private readonly List<(RenderPhase Phase, IRenderPass Pass)> _passes = [];
    private readonly List<IFrameListener> _listeners = [];

    public VkSampleCountFlags MsaaSamples { get; }

    public Renderer(VulkanContext context, GlfwWindow window, CoreDescriptors coreDescriptors)
    {
        _context = context;
        _window = window;
        _coreDescriptors = coreDescriptors;

        MsaaSamples = (VkSampleCountFlags)int.Min(RenderConstants.DefaultMsaa, (int)GetMaxSampleCount());

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

    public void AddPass(IRenderPass pass, RenderPhase phase)
    {
        var i = _passes.FindLastIndex(p => p.Phase <= phase) + 1;
        _passes.Insert(i, (phase, pass));
        if (pass is ITargetConsumer c) c.OnTargetsRecreated(Targets);
    }

    public void AddListener(IFrameListener l) => _listeners.Add(l);

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
            if ((int)(counts & c) != 0) return c;
        }

        return VkSampleCountFlags._1Bit;
    }

    public void DrawFrame(ReadOnlySpan<RenderItem> items, float4x4 viewProj)
    {
        if (!BeginFrame(out var imageIndex, out var isSuboptimal)) return;
        RecordFrame(imageIndex, items, viewProj);
        SubmitFrame(imageIndex);
        PresentFrame(imageIndex, isSuboptimal);

        _frameIndex = (_frameIndex + 1) % RenderConstants.MaxFramesInFlight;
    }

    private bool BeginFrame(out int imageIndex, out bool isSuboptimal)
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

    private void RecordFrame(int imageIndex, ReadOnlySpan<RenderItem> items, float4x4 viewProj)
    {
        var r = _frameResources[_frameIndex];

        var context = new RenderContext
        {
            FrameResources = r,
            Targets = Targets,
            SwapchainImageView = Swapchain.ImageView(imageIndex),
            GlobalDescriptorSet = _coreDescriptors.GlobalSet,
            FrameIndex = _frameIndex,
            ViewProj = viewProj
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

        foreach (var l in _listeners) l.OnPrepare(ref context);
        
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

    private void SubmitFrame(int imageIndex)
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

    private unsafe void PresentFrame(int imageIndex, bool isSuboptimal)
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