using Brutal.GlfwApi;
using Brutal.Pointers;
using Brutal.Strings;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace Armature.Vulkan;

public sealed class VulkanContext : IDisposable
{
    public Instance Instance { get; }
    public VkSurfaceKHR Surface { get; }
    public PhysicalDevice PhysicalDevice { get; }
    public DeviceEx Device { get; }
    public Queue Queue { get; }
    public VkCommandPool CommandPool { get; }
    public VmaAllocator Allocator { get; }

    public unsafe VulkanContext(GlfwWindow window)
    {
        _ = Brutal.VulkanApi.Vulkan.GetInstanceProcAddr;

        var instanceExtensions = new HashSet<String>();
        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions);
        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        for (var i = 0; i < glfwExtensionCount; i++)
            instanceExtensions.Add(Utf8.GetString((Ptr<byte>)glfwExtensions[i]));

        var layers = new HashSet<String>();
        VulkanHelpers.AddValidationLayers(layers);

        Instance = Brutal.VulkanApi.Vulkan.CreateInstance(
            new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = (Int32)VulkanHelpers.Api.VERSION_1_3 },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = instanceExtensions,
                EnabledLayers = layers,
            }
        );

        var result = Glfw.CreateWindowSurface(Instance.VkHandle, window, out var surfaceHandle);

        if (result != (Int32)VkResult.Success)
            throw new InvalidOperationException($"Failed to create Vulkan surface: {result}");

        Surface = new VkSurfaceKHR((nint)surfaceHandle);

        var queueFamily = -1;
        foreach (var candidate in Instance.PhysicalDevices)
        {
            var families = new VkQueueFamilyProperties[candidate.GetQueueFamilyProperties_Count()];
            candidate.GetQueueFamilyProperties_Write(families);

            for (var i = 0; i < families.Length && PhysicalDevice is null; i++)
            {
                if ((families[i].QueueFlags & VkQueueFlags.GraphicsBit) != 0 &&
                    candidate.GetSurfaceSupportKHR(i, Surface))
                    (PhysicalDevice, queueFamily) = (candidate, i);
            }
        }

        if (PhysicalDevice is null)
            throw new InvalidOperationException(
                "No Vulkan device has a queue family that can draw and present to the window");

        var deviceExtensions = new HashSet<String>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, PhysicalDevice);

        var priority = 1.0f;

        var vulkan12Features = new VkPhysicalDeviceVulkan12Features
            { ShaderSampledImageArrayNonUniformIndexing = true, DescriptorBindingPartiallyBound = true };
        var vulkan13Features = new VkPhysicalDeviceVulkan13Features
            { DynamicRendering = true, Next = &vulkan12Features };
        var physicalFeatures = new VkPhysicalDeviceFeatures2()
            { Features = { SamplerAnisotropy = true, FillModeNonSolid = true }, Next = &vulkan13Features };

        Device = PhysicalDevice.CreateDevice(
            new DeviceEx.CreateInfo
            {
                Extensions = deviceExtensions,
                QueueCreateInfos =
                [
                    new VkDeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = queueFamily,
                        QueueCount = 1,
                        QueuePriorities = &priority,
                    }
                ],
                Next = &physicalFeatures
            }
        );

        Queue = Device.GetQueue(queueFamily, 0);

        CommandPool = Device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = Queue.Family,
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit
            },
            null
        );

        Allocator = Device.CreateVmaAllocator(
            new VmaAllocatorCreateInfo
            {
                Instance = Instance,
                PhysicalDevice = PhysicalDevice,
                Device = Device,
                VulkanApiVersion = (UInt32)VulkanHelpers.Api.VERSION_1_3
            }
        );
    }

    public void Dispose()
    {
        Device.DestroyCommandPool(CommandPool, null);
        Device.DestroyVmaAllocator(Allocator);
        Instance.DestroySurfaceKHR(Surface, null);
        Device.Dispose();
        Instance.Dispose();
    }
}