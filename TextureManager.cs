using System.Text;
using Brutal;
using Brutal.KtxApi;
using Brutal.KtxApi.Abstractions;
using Brutal.StbApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using VkFormat = Brutal.VulkanApi.VkFormat;

namespace BrutalVulkan;

public readonly record struct TextureId(UInt32 Value)
{
    public static readonly TextureId Invalid = new(UInt32.MaxValue);
}

public sealed class TextureManager : IDisposable
{
    private UInt32 _nextId = 0;
    
    private readonly Dictionary<TextureId, Texture> _textures = new();
    private readonly Dictionary<String, TextureId> _names = new();

    private readonly List<BufferEx> _pendingStagingBuffers = new();

    private unsafe Texture LoadTexture(String fileName, CommandBuffer commandBuffer, VmaAllocator allocator)
    {
        var filePath = Encoding.UTF8.GetBytes(Path.Join("textures", fileName));
        KtxErrorCode result =
            KtxTexture.CreateFromNamedFile(filePath, KtxTextureCreateFlagBits.LoadImageDataBit, out var image);

        if (result != KtxErrorCode.Success)
            throw new InvalidOperationException($"Could not load file at {filePath}");

        var imageSize = image.GetDataSize();
        var numLevels = (int)image.NumLevels;

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
        Buffer.MemoryCopy(image.GetData(), memory.MapPtr.Pointer, imageSize, imageSize);
        memory.Unmap();

        VkFormat format;
        if (image.ClassId == ClassId.Texture2C)
        {
            var ktx2 = image.AsKtxTexture2Handle();
            format = (VkFormat)ktx2.GetVkFormat();
            if (format == VkFormat.Undefined)
                format = VkFormat.R8G8B8A8UNorm;
        }
        else
        {
            format = VkFormat.R8G8B8A8UNorm;
        }

        var textureInfo = new ImageEx.CreateInfo
        {
            Name = fileName,
            ImageType = VkImageType._2D,
            ImageFormat = format,
            ImageExtent = new VkExtent3D
            {
                Width = (int)image.BaseWidth,
                Height = (int)image.BaseHeight,
                Depth = 1
            },
            ImageMipLevels = numLevels,
            ImageArrayLayers = 1,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageTiling = VkImageTiling.Optimal,
            ImageUsage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit,
            ImageSharingMode = VkSharingMode.Exclusive,
            AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit
        };
        var textureImage = allocator.CreateImage(textureInfo);

        var regions = new VkBufferImageCopy[numLevels];

        App.TransitionImage(
            textureImage.VkImage,
            VkImageLayout.Undefined,
            VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags.TopOfPipeBit,
            VkPipelineStageFlags.TransferBit,
            VkAccessFlags.None,
            VkAccessFlags.TransferWriteBit,
            VkImageAspectFlags.ColorBit,
            commandBuffer,
            numLevels,
            0
        );
        
        for (var i = 0; i < image.NumLevels; i++)
        {
            var width = int.Max(1, (int)image.BaseWidth >> i);
            var height = int.Max(1, (int)image.BaseHeight >> i);
            
            image.GetImageOffset((uint)i, 0, 0, out ulong offset);
            
            regions[i] = new VkBufferImageCopy
            {
                BufferOffset = new ByteSize64(offset),
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource =
                    { AspectMask = VkImageAspectFlags.ColorBit, MipLevel = i, BaseArrayLayer = 0, LayerCount = 1 },
                ImageOffset = new VkOffset3D(0, 0, 0),
                ImageExtent = { Width = width, Height = height, Depth = 1 }
            };
        }

        commandBuffer.CopyBufferToImage(
            stagingBuffer.VkBuffer, textureImage.VkImage, VkImageLayout.TransferDstOptimal, regions);

        App.TransitionImage(
            textureImage.VkImage,
            VkImageLayout.TransferDstOptimal,
            VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags.TransferBit,
            VkPipelineStageFlags.FragmentShaderBit,
            VkAccessFlags.TransferWriteBit,
            VkAccessFlags.ShaderReadBit,
            VkImageAspectFlags.ColorBit,
            commandBuffer,
            numLevels,
            0
        );
        
        var subresourceRange = new VkImageSubresourceRange
        {
            AspectMask = VkImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LayerCount = 1,
            BaseArrayLayer = 0,
            LevelCount = numLevels
        };
        
        var textureImageView = textureImage.CreateImageView(VkImageViewType._2D, subresourceRange,
            textureImage.Allocator.VkAllocator);
        
        image.Dispose();

        return new Texture(textureImage, textureImageView);
    }

    public TextureId AddTexture(String fileName, CommandBuffer commandBuffer, VmaAllocator allocator)
    {
        var textureId = new TextureId(_nextId++);
        
        var texture = LoadTexture(fileName, commandBuffer, allocator);
        var name = Path.GetFileNameWithoutExtension(fileName);

        _textures.Add(textureId, texture);
        _names.Add(name, textureId);
        
        return textureId;
    }

    public Texture GetTexture(TextureId textureId)
    {
        return _textures[textureId];
    }

    public TextureId GetTextureId(String name)
    {
        return _names[name];
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