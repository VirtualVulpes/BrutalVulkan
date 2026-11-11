using Brutal;
using Brutal.StbApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan;

public sealed class Texture : IDisposable
{
    public ImageEx Image { get; }
    public ImageViewEx ImageView { get; }
    public int DescriptorIndex { get; }

    public Texture(ImageEx image, ImageViewEx imageView, int descriptorIndex)
    {
        Image = image;
        ImageView = imageView;
        DescriptorIndex = descriptorIndex;
    }

    public void Dispose()
    {
        ImageView.Dispose();
        Image.Dispose();
    }
}

public sealed class TextureManager : IDisposable
{
    private static int _descriptorIndex = 0;
    private DeviceEx _device;

    private Dictionary<String, Texture> _textures = new();

    private readonly List<BufferEx> _pendingStagingBuffers = new();

    public TextureManager(DeviceEx device)
    {
        _device = device;
    }

    public unsafe void CreateTexture(String filename, ref CommandBuffer commandBuffer, ref VmaAllocator allocator)
    {
        using var image = Stb.Load($"textures/{filename}", 4);

        if (image.IsNull())
            throw new InvalidOperationException("Failed to load texture");

        var imageSize = image.Width * image.Height * 4;
        var bufferInfo = new BufferEx.CreateInfo
        {
            Name = "Staging Buffer",
            BufferSize = ByteSize.Of(imageSize),
            BufferUsage = VkBufferUsageFlags.TransferSrcBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };
        var stagingBuffer = allocator.CreateBuffer(bufferInfo);
        _pendingStagingBuffers.Add(stagingBuffer);
        using var memory = stagingBuffer.Map();
        Buffer.MemoryCopy(image.Data, memory.MapPtr.Pointer, imageSize, imageSize);
        memory.Unmap();

        var textureInfo = new ImageEx.CreateInfo
        {
            Name = "Texture Image",
            ImageType = VkImageType._2D,
            ImageFormat = VkFormat.R8G8B8A8SRGB,
            ImageExtent = new VkExtent3D
            {
                Width = image.Width,
                Height = image.Height,
                Depth = 1
            },
            ImageMipLevels = 1,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };
        var textureImage = allocator.CreateImage(textureInfo);

        App.TransitionImage(
            textureImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.TransferBit,
            VkAccessFlags.None,
            VkAccessFlags.TransferWriteBit,
            VkImageAspectFlags.ColorBit,
            commandBuffer
        );
        App.CopyBufferToImage(ref commandBuffer, stagingBuffer.VkBuffer, ref textureImage, image.Width, image.Height);
        App.TransitionImage(
            textureImage.VkImage,
            VkImageLayout.TransferDstOptimal,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags.TransferBit,
            VkPipelineStageFlags.FragmentShaderBit,
            VkAccessFlags.TransferWriteBit,
            VkAccessFlags.ShaderReadBit,
            VkImageAspectFlags.ColorBit,
            commandBuffer
        );

        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = 1
        };
        var textureImageView = textureImage.CreateImageView(VkImageViewType._2D, subresourceRange,
            textureImage.Allocator.VkAllocator);

        _textures.Add(filename, new Texture(textureImage, textureImageView, _descriptorIndex++));
    }

    public Texture Get(String name)
    {
        return _textures[name];
    }

    // call after submitting queue
    public void ClearStagingBuffers()
    {
        foreach (var buffer in _pendingStagingBuffers)
        {
            buffer.Dispose();
        }
        _pendingStagingBuffers.Clear();
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }
    }
}