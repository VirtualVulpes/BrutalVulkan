using Armature.Vulkan;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace BrutalVulkan.Rendering;

public sealed class CoreDescriptors : IDisposable
{
    private readonly VulkanContext _context;

    private DescriptorPoolEx _descriptorPool;
    private VkSampler _textureSampler;

    public VkDescriptorSet GlobalSet { get; private set; }

    public DescriptorSetLayoutEx GlobalLayout { get; private set; }
    public DescriptorSetLayoutEx PerFrameLayout { get; private set; }

    public CoreDescriptors(VulkanContext context)
    {
        _context = context;

        CreateDescriptorPool();
        CreateTextureSampler();

        CreateGlobalSet();
        CreatePerFrameSet();

    }

    private void CreateDescriptorPool()
    {
        VkDescriptorPoolSize[] pools =
        [
            // textures + screen triangle + color resolve
            new()
            {
                Type = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = Constants.MaxTextures + Constants.MaxFramesInFlight * 50
            },
            // lights buffer
            new()
            {
                Type = VkDescriptorType.StorageBuffer, DescriptorCount = Constants.MaxFramesInFlight
            },
            // camera buffer
            new()
            {
                Type = VkDescriptorType.UniformBuffer, DescriptorCount = Constants.MaxFramesInFlight
            },
            // seed init images
            new()
            {
                Type = VkDescriptorType.StorageImage, DescriptorCount = Constants.MaxFramesInFlight * 20
            }
        ];

        var poolInfo = new DescriptorPoolEx.CreateInfo
        {
            Flags = VkDescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = 100,
            PoolSizes = pools
        };

        _descriptorPool = _context.Device.CreateDescriptorPool(poolInfo, null);
    }

    private unsafe void CreateGlobalSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                DescriptorCount = Constants.MaxTextures,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        VkDescriptorBindingFlags[] flags =
        [
            VkDescriptorBindingFlags.PartiallyBoundBit
        ];

        fixed (VkDescriptorBindingFlags* flagsPtr = flags)
        {
            var bindingFlagsInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo
            {
                BindingCount = bindings.Length,
                BindingFlags = flagsPtr
            };

            var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
            {
                Next = &bindingFlagsInfo,
                Bindings = bindings,
            };

            GlobalLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
        }

        GlobalSet = _context.Device.AllocateDescriptorSet(_descriptorPool, GlobalLayout);
    }

    private void CreatePerFrameSet()
    {
        VkDescriptorSetLayoutBinding[] bindings =
        [
            // lights buffer
            new()
            {
                Binding = 0,
                DescriptorType = VkDescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            },
            // camera buffer
            new()
            {
                Binding = 1,
                DescriptorType = VkDescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = VkShaderStageFlags.FragmentBit
            }
        ];

        var layoutInfo = new DescriptorSetLayoutEx.CreateInfo
        {
            Bindings = bindings,
        };

        PerFrameLayout = _context.Device.CreateDescriptorSetLayout(layoutInfo, null);
    }

    public unsafe void UpdateTextureDescriptor(Texture texture)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = _textureSampler,
            ImageView = texture.ImageView.VkImageView,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal
        };

        ReadOnlySpan<VkWriteDescriptorSet> writes =
        [
            new()
            {
                DstSet = GlobalSet,
                DstBinding = 0,
                DstArrayElement = (int)texture.Id.Value,
                DescriptorCount = 1,
                DescriptorType = VkDescriptorType.CombinedImageSampler,
                ImageInfo = &imageInfo
            }
        ];
        _context.Device.UpdateDescriptorSets(writes, null);
    }

    public VkDescriptorSet AllocatePerFrameDescriptorSet()
    {
        return _context.Device.AllocateDescriptorSet(_descriptorPool, PerFrameLayout);
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

        _textureSampler = _context.Device.CreateSampler(samplerInfo, null);
    }

    public void Dispose()
    {
        _context.Device.DestroyDescriptorPool(_descriptorPool, null);
        _context.Device.DestroySampler(_textureSampler, null);

        _context.Device.DestroyDescriptorSetLayout(GlobalLayout, null);
        _context.Device.DestroyDescriptorSetLayout(PerFrameLayout, null);
    }
}