using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class DescriptorManager : IDisposable
{
    private VulkanContext _context;

    public DescriptorSetLayoutEx DescriptorSetLayout { get; private set; }
    public DescriptorPoolEx DescriptorPool { get; private set; }
    public VkDescriptorSet[] DescriptorSets { get; private set; }

    public VkSampler TextureSampler { get; private set; }

    public DescriptorManager(VulkanContext context)
    {
        _context = context;

        CreateDescriptorSets();

        CreateTextureSampler();
    }

    private unsafe void CreateDescriptorSets()
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

            DescriptorSetLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
        }

        VkDescriptorPoolSize[] pools =
        [
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = Constants.MaxTextures
            },
            new()
            {
                Type = VkDescriptorType.StorageBuffer, DescriptorCount = 1
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 1,
            PoolSizes = pools
        };

        DescriptorPool = _context.Device.CreateDescriptorPool(poolInfo, null);

        DescriptorSets = new VkDescriptorSet[1];

        Span<VkDescriptorSetLayout> layouts = [DescriptorSetLayout];
        fixed (VkDescriptorSetLayout* layoutsPtr = layouts)
        {
            var allocInfo = new VkDescriptorSetAllocateInfo
            {
                DescriptorPool = DescriptorPool,
                DescriptorSetCount = 1,
                SetLayouts = layoutsPtr
            };

            _context.Device.AllocateDescriptorSets(allocInfo, DescriptorSets);
        }
    }

    public unsafe void UpdateTextureDescriptor(Texture texture)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = TextureSampler,
            ImageView = texture.ImageView.VkImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
        [
            new()
            {
                DstSet = DescriptorSets[0],
                DstBinding = 0,
                DstArrayElement = (Int32)texture.Id.Value,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(descriptorWrites, null);
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
        _context.Device.DestroyDescriptorSetLayout(DescriptorSetLayout, null);
        _context.Device.DestroySampler(TextureSampler, null);
    }
}