using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class CommandBufferManager(DeviceEx device, Queue queue, VkCommandPool commandPool)
{
    public CommandBuffer BeginSingleTimeCommands()
    {
        var buffer = device.AllocateCommandBuffer(
            new VkCommandBufferAllocateInfo
            {
                CommandPool = commandPool,
                Level = VkCommandBufferLevel.Primary
            }
        );
        buffer.Begin();

        return buffer;
    }

    public void EndSingleTimeCommands(ref CommandBuffer buffer)
    {
        buffer.End();

        queue.Submit(
            null,
            null,
            [buffer],
            null,
            VkFence.NullHandle
        );
        queue.WaitIdle();

        buffer = default;
    }
}