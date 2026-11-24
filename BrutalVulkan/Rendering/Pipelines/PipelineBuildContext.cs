using Brutal.VulkanApi;

namespace BrutalVulkan.Rendering.Pipelines;

public struct PipelineBuildContext
{
    public VkFormat ColorFormat;
    public VkSampleCountFlags MsaaSamples;
}