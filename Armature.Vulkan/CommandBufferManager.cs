using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Vulkan;

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

    public void EndSingleTimeCommands(CommandBuffer buffer)
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
    
    public static CommandBuffer BeginSingleTimeCommands(DeviceEx device, VkCommandPool commandPool)
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

    public static void EndSingleTimeCommands(CommandBuffer buffer, Queue queue)
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