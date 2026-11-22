using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.ECS;
using BrutalVulkan.Input;
using BrutalVulkan.Rendering;

namespace BrutalVulkan;

public sealed class EntityPicker : IDisposable
{
    private readonly BufferEx[] _buffers;
    private readonly Boolean[] _inFlight;
    private readonly InputManager _inputManager;

    public Entity HoveredEntity { get; private set; } = Entity.Invalid;

    public EntityPicker(DeviceEx device, InputManager inputManager)
    {
        _inputManager = inputManager;

        _buffers = new BufferEx[Constants.MaxFramesInFlight];
        _inFlight = new Boolean[Constants.MaxFramesInFlight];

        for (var i = 0; i < Constants.MaxFramesInFlight; i++)
        {
            _buffers[i] = device.CreateBuffer(new BufferEx.CreateInfo
            {
                Name = "Picking Buffer",
                BufferSize = ByteSize.Of<UInt32>(),
                BufferUsage = VkBufferUsageFlags.TransferDstBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            });
        }
    }

    public void Record(CommandBuffer cmd, Int32 frameIndex, TrackedImage idImage, VkExtent2D extent)
    {
        var cursor = double2.Clamp(_inputManager.CursorPos, double2.Zero, new double2(extent.Width - 1, extent.Height - 1));

        idImage.TransitionTo(cmd, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags.TransferBit,
            VkAccessFlags.TransferReadBit);

        cmd.CopyImageToBuffer(idImage.Image.VkImage, VkImageLayout.TransferSrcOptimal, _buffers[frameIndex].VkBuffer,
        [
            new VkBufferImageCopy
            {
                ImageSubresource = new VkImageSubresourceLayers
                    { AspectMask = VkImageAspectFlags.ColorBit, LayerCount = 1 },
                ImageOffset = new VkOffset3D((Int32)cursor.X, (Int32)cursor.Y, 0),
                ImageExtent = new VkExtent3D { Width = 1, Height = 1, Depth = 1 }
            }
        ]);

        _inFlight[frameIndex] = true;
    }

    public unsafe void Collect(Int32 frameIndex)
    {
        if (!_inFlight[frameIndex]) return;
        _inFlight[frameIndex] = false;

        var memory = _buffers[frameIndex].Map();
        try
        {
            var raw = *(UInt32*)memory.MapPtr.Pointer;
            var entity = new Entity
            {
                Id = new EntityId(raw & 0xFFFF),
                Generation = (UInt32)(raw & ~(0xFFFF)) >> 16
            };
            HoveredEntity = (entity.Id.Value == UInt16.MaxValue) ? Entity.Invalid : entity;
        }
        finally
        {
            memory.Unmap();
        }
    }

    public void Reset()
    {
        HoveredEntity = Entity.Invalid;
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }
    }
}