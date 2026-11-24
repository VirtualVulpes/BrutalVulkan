using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.Editor;

public sealed class EditorDescriptors : IDisposable
{
    private readonly VulkanContext _context;
    private DescriptorPoolEx _descriptorPool;
    private VkSampler _sampler;

    public DescriptorSetLayoutEx OutlineCompositeLayout { get; private set; }
    public DescriptorSetLayoutEx SeedLayout { get; private set; }

    public EditorDescriptors(VulkanContext context)
    {
        _context = context;

        CreateDescriptorPool();
        CreateTextureSampler();

        CreateSeedSets();
        CreateOutlineCompositeSet();
    }

    private void CreateDescriptorPool()
    {
        VkDescriptorPoolSize[] pools =
        [
            // screen triangle
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = Constants.MaxFramesInFlight
            },
            // seed images
            new()
            {
                Type = VkDescriptorType.StorageImage, DescriptorCount = Constants.MaxFramesInFlight * 4
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 4,
            PoolSizes = pools
        };

        _descriptorPool = _context.Device.CreateDescriptorPool(poolInfo, null);
    }

    private void CreateTextureSampler()
    {
        var samplerInfo = new VkSamplerCreateInfo
        {
            MagFilter = VkFilter.Linear,
            MinFilter = VkFilter.Linear,
            MipmapMode = VkSamplerMipmapMode.Linear,
            AddressModeU = VkSamplerAddressMode.ClampToEdge,
            AddressModeV = VkSamplerAddressMode.ClampToEdge,
            AddressModeW = VkSamplerAddressMode.ClampToEdge,
            AnisotropyEnable = true,
            MaxAnisotropy = _context.PhysicalDevice.GetProperties().Limits.MaxSamplerAnisotropy,
            CompareEnable = false,
            CompareOp = VkCompareOp.Always,
            BorderColor = VkBorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 1000.0f
        };

        _sampler = _context.Device.CreateSampler(samplerInfo, null);
    }
    
    private void CreateOutlineCompositeSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            },
            new()
            {
                Binding = 1,
                DescriptorType = VkDescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings,
        };

        OutlineCompositeLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }

    private void CreateSeedSets()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.ComputeBit
            },
            new()
            {
                Binding = 1,
                DescriptorType = VkDescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.ComputeBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings
        };

        SeedLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }

    public unsafe void UpdateOutlineDescriptor(VkDescriptorSet descriptorSet, VkImageView colorImageView,
        VkImageView seedImageView)
    {
        var colorInfo = new VkDescriptorImageInfo
        {
            Sampler = _sampler,
            ImageView = colorImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        var seedInfo = new VkDescriptorImageInfo
        {
            ImageView = seedImageView,
            ImageLayout = VkImageLayout.General
        };

        ReadOnlySpan<VkWriteDescriptorSet> writes =
        [
            new()
            {
                DstSet = descriptorSet,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &colorInfo
            },
            new()
            {
                DstSet = descriptorSet,
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.StorageImage,
                ImageInfo = &seedInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }

    public unsafe void UpdateSeedDescriptor(VkDescriptorSet descriptorSet, VkImageView maskImageView,
        VkImageView seedImageView)
    {
        var maskInfo = new VkDescriptorImageInfo
        {
            ImageView = maskImageView,
            ImageLayout = VkImageLayout.General
        };

        var seedInfo = new VkDescriptorImageInfo
        {
            ImageView = seedImageView,
            ImageLayout = VkImageLayout.General
        };

        ReadOnlySpan<VkWriteDescriptorSet> writes =
        [
            new()
            {
                DstSet = descriptorSet,
                DstBinding = 0,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.StorageImage,
                ImageInfo = &maskInfo
            },
            new()
            {
                DstSet = descriptorSet,
                DstBinding = 1,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.StorageImage,
                ImageInfo = &seedInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }

    public VkDescriptorSet AllocateOutlineDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(_descriptorPool, OutlineCompositeLayout);
    }

    public VkDescriptorSet AllocateSeedDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(_descriptorPool, SeedLayout);
    }

    public void Dispose()
    {
        _context.Device.DestroyDescriptorPool(_descriptorPool, null);
        _context.Device.DestroySampler(_sampler, null);
        
        _context.Device.DestroyDescriptorSetLayout(OutlineCompositeLayout, null);
        _context.Device.DestroyDescriptorSetLayout(SeedLayout, null);
    }
}