using Armature.Rendering;
using Armature.Vulkan;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Editor;

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
                DescriptorCount = RenderConstants.MaxFramesInFlight
            },
            // seed images
            new()
            {
                Type = VkDescriptorType.StorageImage, DescriptorCount = RenderConstants.MaxFramesInFlight * 4
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 4,
            PoolSizes = pools
        };

        _descriptorPool = DescriptorPoolExExtensions.CreateDescriptorPool(_context.Device, poolInfo, null);
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
            MaxAnisotropy = VkInstanceExtensions.GetProperties(_context.PhysicalDevice).Limits.MaxSamplerAnisotropy,
            CompareEnable = false,
            CompareOp = VkCompareOp.Always,
            BorderColor = VkBorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 1000.0f
        };

        _sampler = VkDeviceExtensions.CreateSampler(_context.Device, samplerInfo, null);
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

        OutlineCompositeLayout = DescriptorSetLayoutExExtensions.CreateDescriptorSetLayout(_context.Device, layoutInfo, null);
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

        SeedLayout = DescriptorSetLayoutExExtensions.CreateDescriptorSetLayout(_context.Device, layoutInfo, null);
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
        VkDeviceExtensions.UpdateDescriptorSets(_context.Device, writes, null);
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
        VkDeviceExtensions.UpdateDescriptorSets(_context.Device, writes, null);
    }

    public VkDescriptorSet AllocateOutlineDescriptorSet()
    {
        return DescriptorPoolExExtensions.AllocateDescriptorSet(_context.Device, _descriptorPool, OutlineCompositeLayout);
    }

    public VkDescriptorSet AllocateSeedDescriptorSet()
    {
        return DescriptorPoolExExtensions.AllocateDescriptorSet(_context.Device, _descriptorPool, SeedLayout);
    }

    public void Dispose()
    {
        VkDeviceExtensions.DestroyDescriptorPool(_context.Device, _descriptorPool, null);
        VkDeviceExtensions.DestroySampler(_context.Device, _sampler, null);
        
        VkDeviceExtensions.DestroyDescriptorSetLayout(_context.Device, OutlineCompositeLayout, null);
        VkDeviceExtensions.DestroyDescriptorSetLayout(_context.Device, SeedLayout, null);
    }
}