using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan;

public sealed class Texture : IDisposable
{
    public ImageEx Image { get; }
    public ImageViewEx ImageView { get; }

    public Texture(ImageEx image, ImageViewEx imageView)
    {
        Image = image;
        ImageView = imageView;
    }

    public void Dispose()
    {
        ImageView.Dispose();
        Image.Dispose();
    }
}