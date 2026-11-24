using Brutal.VulkanApi;

namespace Armature.Rendering.Pipelines;

public struct PipelineBuildContext
{
    public VkFormat ColorFormat;
    public VkSampleCountFlags MsaaSamples;
}