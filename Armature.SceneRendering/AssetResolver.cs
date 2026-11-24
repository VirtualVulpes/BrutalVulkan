using Armature.Core;
using Armature.Rendering;
using Armature.Scene;
using Armature.Vulkan;

namespace Armature.SceneRendering;

public sealed class AssetResolver(MeshManager meshes, PipelineManager pipelines, TextureManager textures)
    : IAssetResolver
{
    public MeshId Mesh(string name) => meshes.GetMeshId(name);
    public PipelineId Pipeline(string name) => pipelines.GetPipelineId(name);
    public TextureId Texture(string name) => textures.GetTextureId(name);
}