using Armature.Rendering;
using Armature.SceneRendering;
using Armature.Vulkan;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Microsoft.VisualBasic;

namespace Armature.Editor;

public sealed class EntityPicker : IDisposable, IFrameListener
{
    private readonly EditorTargets _targets;
    private readonly BufferEx[] _buffers;
    private readonly Boolean[] _inFlight;

    private int2? _requested;

    public UInt32? HoveredObjectId { get; private set; }

    public EntityPicker(VulkanContext ctx, EditorTargets targets)
    {
        _targets = targets;
        _buffers = new BufferEx[RenderConstants.MaxFramesInFlight];
        _inFlight = new Boolean[RenderConstants.MaxFramesInFlight];

        for (var i = 0; i < RenderConstants.MaxFramesInFlight; i++)
        {
            _buffers[i] = BufferExExtensions.CreateBuffer(ctx.Device, new BufferEx.CreateInfo
            {
                Name = "Picking Buffer",
                BufferSize = ByteSize.Of<UInt32>(),
                BufferUsage = VkBufferUsageFlags.TransferDstBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            });
        }
    }

    public void Request(int2? pixel)
    {
        _requested = pixel;
        if (pixel is null) HoveredObjectId = null;
    }

    public void Record(CommandBuffer cmd, Int32 frameIndex, TrackedImage idImage, int2 pixel)
    {
        idImage.TransitionTo(cmd, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags.TransferBit,
            VkAccessFlags.TransferReadBit);

        cmd.CopyImageToBuffer(idImage.Image.VkImage, VkImageLayout.TransferSrcOptimal, _buffers[frameIndex].VkBuffer,
        [
            new VkBufferImageCopy
            {
                ImageSubresource = new VkImageSubresourceLayers
                    { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 },
                ImageOffset = new VkOffset3D(pixel.X, pixel.Y, 0),
                ImageExtent = new VkExtent3D { Width = 1, Height = 1, Depth = 1 }
            }
        ]);

        _inFlight[frameIndex] = true;
    }

    private unsafe void Collect(Int32 frameIndex)
    {
        if (!_inFlight[frameIndex]) return;
        _inFlight[frameIndex] = false;

        var memory = _buffers[frameIndex].Map();
        try
        {
            var raw = *(UInt32*)memory.MapPtr.Pointer;
            HoveredObjectId = (raw & 0xFFFF) == 0xFFFF ? null : raw;
        }
        finally
        {
            memory.Unmap();
        }
    }
    
    public void OnResize(RenderTargets targets) => Reset();
    
    public void Reset()
    {
        _requested = null;
        Array.Clear(_inFlight);
        HoveredObjectId = null;
    }

    public void OnFrameBegin(Int32 frameIndex) => Collect(frameIndex);

    public void OnPostRecord(RenderContext ctx)
    {
        if (_requested is not { } p) return;

        var extent = _targets.Extent;
        if (p.X < 0 || p.Y < 0 || p.X >= extent.Width || p.Y >= extent.Height) return;
        
        Record(ctx.CommandBuffer, ctx.FrameIndex, _targets.EntityId.Image, p);
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers)
            buffer.Dispose();
    }
}