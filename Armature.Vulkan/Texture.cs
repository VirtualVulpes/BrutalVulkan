using Armature.Core;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Vulkan;

public enum TextureColorSpace
{
    SRgb,
    Linear
}

public sealed class Texture : IDisposable
{
    public ImageEx Image { get; }
    public ImageViewEx ImageView { get; }
    public TextureId Id { get; }

    public Texture(ImageEx image, ImageViewEx imageView, TextureId id)
    {
        Image = image;
        ImageView = imageView;
        Id = id;
    }

    public void Dispose()
    {
        ImageView.Dispose();
        Image.Dispose();
    }
}