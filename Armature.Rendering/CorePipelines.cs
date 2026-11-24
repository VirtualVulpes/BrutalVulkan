using Armature.Rendering.Pipelines;
using Armature.Vulkan;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

namespace Armature.Rendering;

public static class CorePipelines
{
    public static void Register(DeviceEx device, PipelineManager pipelines, PipelineBuildContext ctx,
        CoreDescriptors descriptors)
    {
        var b = new MeshPipelineBuilder(device, pipelines);
        DescriptorSetLayoutEx[] layouts = [descriptors.GlobalLayout, descriptors.PerFrameLayout];

        MeshPipelineOptions Options(ByteSize frag, VkPolygonMode mode = VkPolygonMode.Fill) => new()
        {
            ColorFormat = ctx.ColorFormat,
            Samples = ctx.MsaaSamples,
            PolygonMode = mode,
            FragmentPushSize = frag,
            Layouts = layouts
        };

        var tex = ByteSize.Of<PushConstantBlocks.TextureFragmentBlock>();
        var col = ByteSize.Of<PushConstantBlocks.ColorFragmentBlock>();

        b.Add("Flat Texture", "world_space.vert", "flat_texture.frag", Options(tex));
        b.Add("Flat Color", "flat_color.vert", "flat_color.frag", Options(col));
        b.Add("Wireframe", "flat_color.vert", "flat_color.frag", Options(col, VkPolygonMode.Line));
        b.Add("Phong Texture", "world_space.vert", "phong_texture.frag", Options(tex));
        b.Add("Phong Color", "world_space.vert", "phong_color.frag", Options(col));
    }
}