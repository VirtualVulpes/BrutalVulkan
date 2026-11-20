using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using BrutalVulkan.ECS;

namespace BrutalVulkan.Rendering;

public sealed class DescriptorManager : IDisposable
{
    private VulkanContext _context;

    public DescriptorPoolEx DescriptorPool { get; private set; }

    public DescriptorSetLayoutEx GlobalLayout { get; private set; }
    public VkDescriptorSet GlobalSet { get; private set; }

    public DescriptorSetLayoutEx OutlineCompositeLayout { get; private set; }
    public DescriptorSetLayoutEx SeedLayout { get; private set; }
    public DescriptorSetLayoutEx SeedAToBLayout { get; private set; }
    public DescriptorSetLayoutEx SeedBToALayout { get; private set; }
    public DescriptorSetLayoutEx AtmosphereLayout { get; private set; }

    public VkSampler TextureSampler { get; private set; }

    public DescriptorManager(VulkanContext context)
    {
        _context = context;

        CreateDescriptorPool();
        
        CreateGlobalDescriptorSet();
        CreateOutlineCompositeSet();
        CreateOutlineDescriptorSets();
        CreateAtmosphereDescriptorSet();

        CreateTextureSampler();
    }

    private void CreateDescriptorPool()
    {
        VkDescriptorPoolSize[] pools =
        [
            // textures + screen triangle + color resolve
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = Constants.MaxTextures + Constants.MaxFramesInFlight * 50
            },
            // lights buffer
            new()
            {
                Type = VkDescriptorType.StorageBuffer, DescriptorCount = 1
            },
            // seed init images
            new()
            {
                Type = VkDescriptorType.StorageImage, DescriptorCount = Constants.MaxFramesInFlight * 20
            },
            // camera buffer
            new()
            {
                Type = VkDescriptorType.UniformBuffer, DescriptorCount = 1
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 100,
            PoolSizes = pools
        };

        DescriptorPool = _context.Device.CreateDescriptorPool(poolInfo, null);
    }

    private unsafe void CreateGlobalDescriptorSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = Constants.MaxTextures,
                StageFlags = VkShaderStageFlags.FragmentBit
            },
            new()
            {
                Binding = 1,
                DescriptorType = VkDescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            },
            new()
            {
                Binding = 2,
                DescriptorType = VkDescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        VkDescriptorBindingFlags[] flags =
        [
            VkDescriptorBindingFlags.PartiallyBoundBit,
            VkDescriptorBindingFlags.None,
            VkDescriptorBindingFlags.None
        ];

        fixed (VkDescriptorBindingFlags* flagsPtr = flags)
        {
            var bindingFlagsInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo
            {
                BindingCount = flags.Length,
                BindingFlags = flagsPtr
            };

            var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
            {
                Next = &bindingFlagsInfo,
                Bindings = bindings,
            };

            GlobalLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
        }

        GlobalSet = _context.Device.AllocateDescriptorSet(DescriptorPool, GlobalLayout);
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
    
    private void CreateOutlineDescriptorSets()
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
            Bindings = bindings,
        };

        SeedLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
        SeedAToBLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
        SeedBToALayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }
    
    private void CreateAtmosphereDescriptorSet()
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
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings,
        };

        AtmosphereLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }
    
    public unsafe void UpdateTextureDescriptor(Texture texture)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
            ImageView = texture.ImageView.VkImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        ReadOnlySpan<VkWriteDescriptorSet> writes =
        [
            new()
            {
                DstSet = GlobalSet,
                DstBinding = 0,
                DstArrayElement = (Int32)texture.Id.Value,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }
    
    public unsafe void UpdateOutlineDescriptor(VkDescriptorSet descriptorSet, VkImageView colorImageView,
        VkImageView seedImageView)
    {
        var colorInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
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

    public unsafe void UpdateSeedDescriptor(VkDescriptorSet descriptorSet, VkImageView maskImageView, VkImageView seedImageView)
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
    
    public unsafe void UpdateAtmosphereDescriptor(VkDescriptorSet descriptorSet, VkImageView colorImageView, VkImageView depthImageView)
    {
        var colorInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
            ImageView = colorImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };
        
        var depthInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
            ImageView = depthImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
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
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &depthInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }
    
    public VkDescriptorSet AllocateOutlineDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, OutlineCompositeLayout);
    }
    
    public VkDescriptorSet AllocateSeedDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, SeedLayout);
    }

    public VkDescriptorSet AllocateSeedAToBDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, SeedAToBLayout);
    }
    
    public VkDescriptorSet AllocateSeedBToADescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, SeedBToALayout);
    }
    
    public VkDescriptorSet AllocateAtmosphereDescriptor()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, AtmosphereLayout);
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

        TextureSampler = _context.Device.CreateSampler(samplerInfo, null);
    }

    public void Dispose()
    {
        _context.Device.DestroyDescriptorPool(DescriptorPool, null);
        _context.Device.DestroySampler(TextureSampler, null);

        _context.Device.DestroyDescriptorSetLayout(GlobalLayout, null);
        _context.Device.DestroyDescriptorSetLayout(OutlineCompositeLayout, null);
        _context.Device.DestroyDescriptorSetLayout(SeedLayout, null);
        _context.Device.DestroyDescriptorSetLayout(SeedAToBLayout, null);
        _context.Device.DestroyDescriptorSetLayout(SeedBToALayout, null);
        _context.Device.DestroyDescriptorSetLayout(AtmosphereLayout, null);
    }
}