using Armature.Core;

namespace Armature.Scene;

public interface IAssetResolver
{
    MeshId Mesh(String name);
    PipelineId Pipeline(String name);
    TextureId Texture(String name);
}