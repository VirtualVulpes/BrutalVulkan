using System.Text;
using Brutal;
using Brutal.KtxApi;
using Brutal.KtxApi.Abstractions;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using BrutalVulkan.Utility;
using VkFormat = Brutal.VulkanApi.VkFormat;

namespace BrutalVulkan;

public readonly record struct TextureId(UInt32 Value)
{
    public static readonly TextureId Invalid = new(UInt32.MaxValue);
}

public sealed class TextureManager : IDisposable
{
    private UInt32 _nextId;

    private readonly Dictionary<TextureId, Texture> _textures = new();
    private readonly Dictionary<String, TextureId> _names = new();

    private readonly List<BufferEx> _pendingStagingBuffers = [];

    private readonly Boolean _supportsBc7;

    public TextureManager(Boolean supportsBc7)
    {
        _supportsBc7 = supportsBc7;
    }

    private unsafe Texture LoadTexture(String fileName, CommandBuffer commandBuffer, VmaAllocator allocator,
        TextureColorSpace colorSpace, TextureId id)
    {
        var filePath = Path.Join("textures", fileName);
        KtxErrorCode result =
            KtxTexture.CreateFromNamedFile(Encoding.UTF8.GetBytes(filePath), KtxTextureCreateFlagBits.LoadImageDataBit,
                out var image);

        if (result != KtxErrorCode.Success)
            throw new InvalidOperationException($"Could not load file at {filePath}");

        if (image.ClassId != ClassId.Texture2C)
            throw new InvalidOperationException("Only KTX2 texture files are supported");

        var ktx2 = image.AsKtxTexture2Handle();

        if (ktx2.NeedsTranscoding())
        {
            result = ktx2.TranscodeBasis(_supportsBc7 ? KtxTranscodeFmt.Bc7Rgba : KtxTranscodeFmt.Rgba32,
                KtxTranscodeFlagBits.HighQuality);

            if (result != KtxErrorCode.Success)
                throw new InvalidOperationException($"Could not transcode file at {filePath}");
        }

        var format = (VkFormat)ktx2.GetVkFormat();
        if (format == VkFormat.Undefined)
        {
            var srgb = (colorSpace == TextureColorSpace.SRgb);

            if (_supportsBc7)
            {
                format = srgb ? VkFormat.Bc7SRGBBlock : VkFormat.Bc7UNormBlock;
            }
            else
            {
                format = srgb ? VkFormat.R8G8B8A8SRGB : VkFormat.R8G8B8A8UNorm;
            }
        }

        var imageSize = ktx2.GetDataSize();
        var numLevels = (Int32)ktx2.NumLevels;

        var bufferInfo = new BufferEx.CreateInfo
        {
            Name = "Staging Buffer",
            BufferSize = ByteSize.Of(imageSize),
            BufferUsage = VkBufferUsageFlags.TransferSrcBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };

        var stagingBuffer = allocator.CreateBuffer(bufferInfo);
        _pendingStagingBuffers.Add(stagingBuffer);

        var memory = stagingBuffer.Map();
        Buffer.MemoryCopy(ktx2.GetData(), memory.MapPtr.Pointer, imageSize, imageSize);
        memory.Unmap();

        var textureInfo = new ImageEx.CreateInfo
        {
            Name = fileName,
            ImageType = VkImageType._2D,
            ImageFormat = format,
            ImageExtent = new VkExtent3D
            {
                Width = (Int32)ktx2.BaseWidth,
                Height = (Int32)ktx2.BaseHeight,
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

        ImageUtility.TransitionImage(
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

        for (var i = 0; i < ktx2.NumLevels; i++)
        {
            var width = Int32.Max(1, (Int32)ktx2.BaseWidth >> i);
            var height = Int32.Max(1, (Int32)ktx2.BaseHeight >> i);

            ktx2.GetImageOffset((UInt32)i, 0, 0, out var offset);

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

        ImageUtility.TransitionImage(
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

        ktx2.Dispose();

        return new Texture(textureImage, textureImageView, id);
    }

    public TextureId AddTexture(String fileName, CommandBuffer commandBuffer, VmaAllocator allocator,
        TextureColorSpace colorSpace)
    {
        var textureId = new TextureId(_nextId++);

        var texture = LoadTexture(fileName, commandBuffer, allocator, colorSpace, textureId);
        
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