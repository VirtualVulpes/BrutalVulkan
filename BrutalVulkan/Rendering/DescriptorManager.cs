using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class DescriptorManager : IDisposable
{
    private VulkanContext _context;

    public DescriptorPoolEx DescriptorPool { get; private set; }

    public DescriptorSetLayoutEx GlobalLayout { get; private set; }
    public VkDescriptorSet GlobalSet { get; private set; }

    public DescriptorSetLayoutEx ComputeLayout { get; private set; }
    public DescriptorSetLayoutEx DisplayLayout { get; private set; }

    public VkSampler TextureSampler { get; private set; }

    public DescriptorManager(VulkanContext context)
    {
        _context = context;

        CreateDescriptorPool();
        CreateGlobalDescriptorSet();
        CreateComputeDescriptorSet();
        CreateDisplayDescriptorSet();

        CreateTextureSampler();
    }

    private void CreateDescriptorPool()
    {
        VkDescriptorPoolSize[] pools =
        [
            // textures + screen triangle
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = Constants.MaxTextures + Constants.MaxFramesInFlight
            },
            // lights buffer
            new()
            {
                Type = VkDescriptorType.StorageBuffer, DescriptorCount = 1
            },
            // test compute image
            new()
            {
                Type = VkDescriptorType.StorageImage, DescriptorCount = Constants.MaxFramesInFlight
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 1 + Constants.MaxFramesInFlight * 2,
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
            }
        ];

        VkDescriptorBindingFlags[] flags =
        [
            VkDescriptorBindingFlags.PartiallyBoundBit,
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

    private void CreateComputeDescriptorSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.ComputeBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings,
        };

        ComputeLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }

    private void CreateDisplayDescriptorSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings,
        };

        DisplayLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
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

    public unsafe void UpdateComputeDescriptor(VkDescriptorSet descriptorSet, VkImageView imageView)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            ImageView = imageView,
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
                ImageInfo = &imageInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }
    
    public unsafe void UpdateDisplayDescriptor(VkDescriptorSet descriptorSet, VkImageView imageView)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
            ImageView = imageView,
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
                ImageInfo = &imageInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }

    public VkDescriptorSet AllocateComputeDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, ComputeLayout);
    }

    
    public VkDescriptorSet AllocateDisplayDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(DescriptorPool, DisplayLayout);
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
        _context.Device.DestroyDescriptorSetLayout(ComputeLayout, null);
        _context.Device.DestroyDescriptorSetLayout(DisplayLayout, null);
    }
}