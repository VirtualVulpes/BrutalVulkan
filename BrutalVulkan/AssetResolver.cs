using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Pipelines;

namespace BrutalVulkan;

public sealed class AssetResolver(MeshManager meshes, PipelineManager pipelines, TextureManager textures)
{
    private readonly MeshManager _meshes = meshes;
    private readonly PipelineManager _pipelines = pipelines;
    private readonly TextureManager _textures = textures;

    public MeshId Mesh(String name)
    {
        return _meshes.GetMeshId(name);
    }
    
    public PipelineId Pipeline(String name)
    {
        return _pipelines.GetPipelineId(name);
    }
    
    public TextureId Texture(String name)
    {
        return _textures.GetTextureId(name);
    }
}