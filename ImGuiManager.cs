using Brutal;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan;

public sealed class ImGuiManager : IDisposable
{
    private VkSampler _fontSampler;
    private readonly BufferEx[] _vertexBuffers = new BufferEx[Constants.MaxFramesInFlight];
    private readonly BufferEx[] _indexBuffers = new BufferEx[Constants.MaxFramesInFlight];
    private readonly Int32[] _vertexCounts = new Int32[Constants.MaxFramesInFlight];
    private readonly Int32[] _indexCounts = new Int32[Constants.MaxFramesInFlight];
    private ImageEx _fontImage;
    private ImageViewEx _fontImageView;

    private readonly ImGuiContextPtr _context;

    private readonly VmaAllocator _allocator;
    private readonly DeviceEx _device;
    private readonly CommandBufferManager _cmdManager;

    private DescriptorPoolEx _descriptorPool;
    
    public DescriptorSetLayoutEx DescriptorSetLayout { get; private set; }
    public VkDescriptorSet DescriptorSet { get; private set; }

    public BufferEx GetVertexBuffer(Int32 frameIndex)
        => _vertexBuffers[frameIndex];

    public BufferEx GetIndexBuffer(Int32 frameIndex)
        => _indexBuffers[frameIndex];

    public ImGuiManager(VmaAllocator allocator, DeviceEx device, float2 displaySize, CommandBufferManager cmdManager)
    {
        _allocator = allocator;
        _device = device;
        _cmdManager = cmdManager;

        _context = ImGui.CreateContext();
        if (_context.IsNull())
            throw new InvalidOperationException("Failed to create ImGui context.");

        var io = ImGui.GetIO();
        io.IniFilename = null; // stop ImGui.ini write
        io.DisplaySize = displaySize;

        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        ImGui.StyleColorsDark();

        CreateResources();
    }

    private void CreateResources()
    {
        var binding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit
        };

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = [binding]
        };

        DescriptorSetLayout = _device.CreateDescriptorSetLayout(layoutInfo, null);

        var poolSize = new VkDescriptorPoolSize
        {
            Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = 1
        };

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 1,
            PoolSizes = [poolSize]
        };

        _descriptorPool = _device.CreateDescriptorPool(poolInfo, null);
        
        DescriptorSet = _device.AllocateDescriptorSet(_descriptorPool, DescriptorSetLayout);
    }

    public void NewFrame()
    {
        ImGui.NewFrame();
    }

    public void RenderFrame(Int32 frameIndex)
    {
        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid) return;

        UpdateBuffers(frameIndex);

        if (drawData.Textures.IsNull()) return;

        foreach (var texture in drawData.Textures.Value.Where(texture => texture.Status != ImTextureStatus.OK))
        {
            UpdateTexture(texture);
        }
    }

    private unsafe void UpdateTexture(ImTextureDataPtr texture)
    {
        if (texture.Status is not (ImTextureStatus.WantCreate or ImTextureStatus.WantUpdates)) return;

        var format = (texture.BytesPerPixel == 4) ? VkFormat.R8G8B8A8UNorm : VkFormat.R8UNorm;

        if (texture.Status is ImTextureStatus.WantCreate)
        {
            if (texture.Pixels.IsNull()) return;

            var createInfo = new ImageEx.CreateInfo
            {
                Name = "ImGui Font Image",
                ImageType = VkImageType._2D,
                ImageFormat = format,
                ImageExtent = new VkExtent3D
                {
                    Width = texture.Width,
                    Height = texture.Height,
                    Depth = 1
                },
                ImageMipLevels = 1,
                ImageArrayLayers = 1,
                ImageSamples = VkSampleCountFlags._1Bit,
                ImageTiling = VkImageTiling.Optimal,
                ImageUsage = VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
                ImageSharingMode = VkSharingMode.Exclusive,
                AllocRequiredProperties = VkMemoryPropertyFlags.DeviceLocalBit,
            };
            _fontImage = _allocator.CreateImage(createInfo);

            var subresourceRange = new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                LayerCount = 1,
                LevelCount = 1
            };
            _fontImageView =
                _fontImage.CreateImageView(VkImageViewType._2D, subresourceRange, _fontImage.Allocator.VkAllocator);

            var samplerInfo = new VkSamplerCreateInfo
            {
                MagFilter = VkFilter.Linear,
                MinFilter = VkFilter.Linear,
                MipmapMode = VkSamplerMipmapMode.Linear,
                AddressModeU = VkSamplerAddressMode.ClampToEdge,
                AddressModeV = VkSamplerAddressMode.ClampToEdge,
                AddressModeW = VkSamplerAddressMode.ClampToEdge,
                AnisotropyEnable = false,
                MaxAnisotropy = 1.0f,
                CompareEnable = false,
                CompareOp = VkCompareOp.Always,
                BorderColor = VkBorderColor.FloatOpaqueWhite,
                UnnormalizedCoordinates = false,
                MipLodBias = 0.0f,
                MinLod = 0.0f,
                MaxLod = 0.0f
            };
            _fontSampler = _device.CreateSampler(samplerInfo, null);

            var imageDescriptor = new VkDescriptorImageInfo
            {
                ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                ImageView = _fontImageView.VkImageView,
                Sampler = _fontSampler
            };

            var writeSet = new VkWriteDescriptorSet
            {
                DstSet = DescriptorSet,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageDescriptor,
                DstBinding = 0
            };

            _device.UpdateDescriptorSets([writeSet], []);
        }

        var uploadSize = texture.Width * texture.Height * texture.BytesPerPixel;

        var bufferInfo = new BufferEx.CreateInfo
        {
            Name = "ImGui Staging Buffer",
            BufferSize = ByteSize.Of(uploadSize),
            BufferUsage = VkBufferUsageFlags.TransferSrcBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        };

        var stagingBuffer = _allocator.CreateBuffer(bufferInfo);

        var memory = stagingBuffer.Map();
        Buffer.MemoryCopy(texture.Pixels, memory.MapPtr.Pointer, uploadSize, uploadSize);
        memory.Unmap();

        var cmd = _cmdManager.BeginSingleTimeCommands();

        App.TransitionImage(_fontImage.VkImage, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TransferBit, VkAccessFlags.None,
            VkAccessFlags.TransferWriteBit, VkImageAspectFlags.ColorBit, cmd);
        App.CopyBufferToImage(cmd, stagingBuffer.VkBuffer, _fontImage, texture.Width, texture.Height);
        App.TransitionImage(_fontImage.VkImage, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.FragmentShaderBit, VkAccessFlags.TransferWriteBit,
            VkAccessFlags.ShaderReadBit, VkImageAspectFlags.ColorBit, cmd);

        _cmdManager.EndSingleTimeCommands(ref cmd);
        stagingBuffer.Dispose();

        texture.SetTexID((ImTextureID)DescriptorSet.VkHandle);
        texture.SetStatus(ImTextureStatus.OK);
    }

    private unsafe void UpdateBuffers(Int32 frameIndex)
    {
        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0) return;

        var vertexBufferSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
        var indexBufferSize = drawData.TotalIdxCount * sizeof(UInt16);

        if (drawData.TotalVtxCount > _vertexCounts[frameIndex])
        {
            if (_vertexBuffers[frameIndex].IsNotNull())
                _vertexBuffers[frameIndex].Dispose();
            _vertexBuffers[frameIndex] = default;

            var vertexCreateInfo = new BufferEx.CreateInfo
            {
                Name = "ImGui Vertex Buffer",
                BufferFlags = VkBufferCreateFlags.None,
                BufferSize = ByteSize.Of(vertexBufferSize),
                BufferUsage = VkBufferUsageFlags.VertexBufferBit,
                BufferSharingMode = VkSharingMode.Exclusive,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };

            _vertexBuffers[frameIndex] = _allocator.CreateBuffer(vertexCreateInfo);
            _vertexCounts[frameIndex] = drawData.TotalVtxCount;
        }

        if (drawData.TotalIdxCount > _indexCounts[frameIndex])
        {
            if (_indexBuffers[frameIndex].IsNotNull())
                _indexBuffers[frameIndex].Dispose();
            _indexBuffers[frameIndex] = default;

            var indexCreateInfo = new BufferEx.CreateInfo
            {
                Name = "ImGui Index Buffer",
                BufferFlags = VkBufferCreateFlags.None,
                BufferSize = ByteSize.Of(indexBufferSize),
                BufferUsage = VkBufferUsageFlags.IndexBufferBit,
                BufferSharingMode = VkSharingMode.Exclusive,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };

            _indexBuffers[frameIndex] = _allocator.CreateBuffer(indexCreateInfo);
            _indexCounts[frameIndex] = drawData.TotalIdxCount;
        }

        if (drawData is not { TotalVtxCount: > 0, TotalIdxCount: > 0 }) return;

        var vtxMemory = _vertexBuffers[frameIndex].Map();
        var idxMemory = _indexBuffers[frameIndex].Map();

        var vtxDst = (ImDrawVert*)vtxMemory.MapPtr;
        var idxDst = (UInt16*)idxMemory.MapPtr;

        foreach (var cmdList in drawData.CmdLists)
        {
            Buffer.MemoryCopy(cmdList.VtxBuffer.Data, vtxDst, cmdList.VtxBuffer.Count * sizeof(ImDrawVert),
                cmdList.VtxBuffer.Count * sizeof(ImDrawVert));
            Buffer.MemoryCopy(cmdList.IdxBuffer.Data, idxDst, cmdList.IdxBuffer.Count * sizeof(UInt16),
                cmdList.IdxBuffer.Count * sizeof(UInt16));
            vtxDst += cmdList.VtxBuffer.Count;
            idxDst += cmdList.IdxBuffer.Count;
        }

        vtxMemory.Unmap();
        idxMemory.Unmap();
    }

    public void Dispose()
    {
        if(_fontSampler.IsNotNull())
            _device.DestroySampler(_fontSampler, null);
        if (_fontImageView.IsNotNull())
            _fontImageView.Dispose();
        if (_fontImage.IsNotNull())
            _fontImage.Dispose();

        for (var i = 0; i < _vertexBuffers.Length; i++)
        {
            if (_vertexBuffers[i].IsNotNull())
                _vertexBuffers[i].Dispose();
            if (_indexBuffers[i].IsNotNull())
                _indexBuffers[i].Dispose();
        }

        _device.DestroyDescriptorSetLayout(DescriptorSetLayout, null);
        _device.DestroyDescriptorPool(_descriptorPool, null);

        ImGui.DestroyContext(_context);
    }

    public void OnFramebufferResize(int2 size)
    {
        ImGui.GetIO().DisplaySize = new float2(size.X, size.Y);
    }
}