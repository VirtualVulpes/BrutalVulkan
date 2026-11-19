using BrutalVulkan.ECS.Components;
using BrutalVulkan.Rendering.Meshes;

namespace BrutalVulkan.ECS.Systems;

public class NodeSystem : ISystem
{
    private World _world;
    private MeshManager _meshManager;

    public NodeSystem(World world, MeshManager meshManager)
    {
        _world = world;
        _meshManager = meshManager;
    }

    public void CreateMesh(Entity e)
    {
        if (_world.HasComponent<MeshComponent>(e)) return;

        var node = _world.GetComponent<NodeComponent>(e);

        if (!_meshManager.HasMesh(node.ToString()))
            _meshManager.AddMesh(node.ToString(), CubeSphere.CreateChunkMesh(node.Face, node.Level, node.X, node.Y));

        _world.AddComponent(e, new MeshComponent
        {
            MeshId = _meshManager.GetMeshId(node.ToString())
        });
    }

    public void Update(float deltaTime)
    {
    }
}