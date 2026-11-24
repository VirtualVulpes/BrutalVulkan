using Armature.Vulkan;
using Brutal.GlfwApi;
using Brutal.VulkanApi;

namespace Armature.Rendering;

public sealed class Swapchain : IDisposable
{
    private static readonly VkCompositeAlphaFlagsKHR[] AlphaModes =
    [
        VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
        VkCompositeAlphaFlagsKHR.PreMultipliedBitKHR,
        VkCompositeAlphaFlagsKHR.PostMultipliedBitKHR,
        VkCompositeAlphaFlagsKHR.InheritBitKHR
    ];

    private readonly VulkanContext _context;

    private VkImage[] _swapchainImages;
    private VkImageView[] _swapchainImageViews;

    private VkSemaphore[] _renderFinished;

    public VkSwapchainKHR Handle { get; private set; }

    public VkSurfaceFormatKHR SurfaceFormat { get; private set; }

    public VkExtent2D Extent { get; private set; }

    public VkImage Image(Int32 index) => _swapchainImages[index];
    public VkImageView ImageView(Int32 index) => _swapchainImageViews[index];
    public VkSemaphore RenderFinished(Int32 index) => _renderFinished[index];

    public Swapchain(VulkanContext context, GlfwWindow window)
    {
        _context = context;

        Create(window);
        CreateResources();
    }

    private void Create(GlfwWindow window)
    {
        var capabilities = _context.PhysicalDevice.GetSurfaceCapabilitiesKHR(_context.Surface);
        
        Extent = capabilities.CurrentExtent;
        if (Extent is { Width: -1, Height: -1 })
            Extent = new VkExtent2D(
                Math.Clamp((int)window.FramebufferSize.X, capabilities.MinImageExtent.Width,
                    capabilities.MaxImageExtent.Width),
                Math.Clamp((int)window.FramebufferSize.Y, capabilities.MinImageExtent.Height,
                    capabilities.MaxImageExtent.Height)
            );

        var imageCount = Math.Max(capabilities.MinImageCount, 2);
        if (capabilities.MaxImageCount > 0)
            imageCount = Math.Min(imageCount, capabilities.MaxImageCount);

        var compositeAlpha = AlphaModes.First(mode => (capabilities.SupportedCompositeAlpha & mode) != 0);

        SurfaceFormat = ChooseSurfaceFormat();

        Handle = _context.Device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _context.Surface,
                MinImageCount = imageCount,
                ImageFormat = SurfaceFormat.Format,
                ImageColorSpace = SurfaceFormat.ColorSpace,
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
    }

    public void Recreate(GlfwWindow window)
    {
        Cleanup();
        Create(window);
        CreateResources();
    }

    private void CreateImageViews()
    {
        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            _swapchainImageViews[i] = _context.Device.CreateImageView(
                new VkImageViewCreateInfo
                {
                    Image = _swapchainImages[i],
                    ViewType = VkImageViewType._2D,
                    Format = SurfaceFormat.Format,
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

    private void CreateResources()
    {
        _swapchainImages = new VkImage[_context.Device.GetSwapchainImagesKHR_Count(Handle)];
        _context.Device.GetSwapchainImagesKHR_Write(Handle, _swapchainImages);

        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        CreateImageViews();

        _renderFinished =
            [.. _swapchainImages.Select(_ => _context.Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null))];
    }

    private VkSurfaceFormatKHR ChooseSurfaceFormat()
    {
        Span<VkSurfaceFormatKHR> surfaceFormats =
            stackalloc VkSurfaceFormatKHR[_context.PhysicalDevice.GetSurfaceFormatsKHR_Count(_context.Surface)];
        _context.PhysicalDevice.GetSurfaceFormatsKHR_Write(_context.Surface, surfaceFormats);

        if (surfaceFormats.IsEmpty)
            throw new InvalidOperationException("No surface formats available.");

        foreach (var format in surfaceFormats)
        {
            if (format is { Format: VkFormat.B8G8R8A8SRGB, ColorSpace: VkColorSpaceKHR.SRGBNonlinearKHR })
                return format;
        }

        return surfaceFormats[0];
    }

    private void Cleanup()
    {
        foreach (var semaphore in _renderFinished)
            _context.Device.DestroySemaphore(semaphore, null);
        _renderFinished = [];

        foreach (var imageView in _swapchainImageViews)
            _context.Device.DestroyImageView(imageView, null);
        _swapchainImageViews = [];

        _context.Device.DestroySwapchainKHR(Handle, null);
        Handle = VkSwapchainKHR.NullHandle;
    }

    public void Dispose()
    {
        Cleanup();
    }
}