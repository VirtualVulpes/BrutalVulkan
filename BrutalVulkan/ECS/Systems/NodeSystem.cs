using BrutalVulkan.ECS.Components;
using BrutalVulkan.Rendering.Meshes;

namespace BrutalVulkan.ECS.Systems;

public sealed class NodeSystem : ISystem
{
    private World _world;
    private MeshManager _meshManager;

    // node key to mesh id
    private Dictionary<UInt64, MeshId> _meshIds = new();
    // node key to chunk
    private Dictionary<UInt64, Entity> _chunks = new();

    private static UInt64 Key(NodeComponent node) => ((UInt64)node.Face << 61) | ((UInt64)node.Level << 56) | ((UInt64)node.X << 28) | ((UInt64)node.Y << 0); 
    
    public NodeSystem(World world, MeshManager meshManager)
    {
        _world = world;
        _meshManager = meshManager;
    }

    public void CreateMesh(Entity e)
    {
        if (_world.HasComponent<MeshComponent>(e)) return;

        var node = _world.GetComponent<NodeComponent>(e);

        if (!_meshIds.ContainsKey(Key(node)))
        {
            var meshId = _meshManager.AddMesh(CubeSphere.CreateChunkMesh(node.Face, node.Level, node.X, node.Y));
            _meshIds.Add(Key(node), meshId);
        }

        _world.AddComponent(e, new MeshComponent
        {
            MeshId = _meshIds[Key(node)]
        });
    }

    public void IncreaseLod(Entity entity)
    {
        var node = _world.GetComponent<NodeComponent>(entity);

        if (node.Level >= 28)
            return;

        var transform = _world.GetComponent<TransformComponent>(entity);
        var pipeline = _world.GetComponent<PipelineComponent>(entity);
        var color = _world.GetComponent<ColorComponent>(entity);
        var parent = _world.GetComponent<ParentComponent>(entity); 

        _chunks.Remove(Key(node));
        
        for (UInt32 y = 0; y < 2; y++)
        {
            for (UInt32 x = 0; x < 2; x++)
            {
                var child = _world.CreateEntity();
                
                var childNode = new NodeComponent
                {
                    Face = node.Face,
                    Level = node.Level + 1,
                    X = node.X * 2 + x,
                    Y = node.Y * 2 + y
                };
                
                _world.AddComponent(child, childNode);
                _world.AddComponent(child, transform);
                _world.AddComponent(child, pipeline);
                _world.AddComponent(child, color);
                _world.AddComponent(child, parent);
                
                CreateMesh(child);

                _chunks[Key(childNode)] = child;
            }
        }

        _world.DestroyEntity(entity);
    }
    
    public void DecreaseLod(Entity entity)
    {
        var node = _world.GetComponent<NodeComponent>(entity);

        if (node.Level == 0) return;
        
        var parentNode = new NodeComponent
        {
            Face = node.Face,
            Level = node.Level - 1,
            X = node.X / 2,
            Y = node.Y / 2
        };

        var children = new[]
        {
            node,
            node with { X = node.X ^ 1 },
            node with { Y = node.Y ^ 1 },
            node with { X = node.X ^ 1, Y = node.Y ^ 1 }
        };

        var childEntities = new Entity[4];

        for (var i = 0; i < children.Length; i++)
        {
            if (!_chunks.TryGetValue(Key(children[i]), out childEntities[i])) return;
        }
        
        var transform = _world.GetComponent<TransformComponent>(entity);
        var pipeline = _world.GetComponent<PipelineComponent>(entity);
        var color = _world.GetComponent<ColorComponent>(entity); 
        var parent = _world.GetComponent<ParentComponent>(entity); 
        
        var parentEntity = _world.CreateEntity();
        
        _world.AddComponent(parentEntity, parentNode);
        _world.AddComponent(parentEntity, transform);
        _world.AddComponent(parentEntity, pipeline);
        _world.AddComponent(parentEntity, color);
        _world.AddComponent(parentEntity, parent);
        
        CreateMesh(parentEntity);

        foreach (var childEntity in childEntities)
        {
            var childNode = _world.GetComponent<NodeComponent>(childEntity);

            _chunks.Remove(Key(childNode));
            _world.DestroyEntity(childEntity);
        }
        
        _chunks[Key(parentNode)] = parentEntity;
    }
    
    public void Start()
    {
        foreach (var item in _world.Query<NodeComponent>())
        {
            _chunks[Key(item.C1)] = item.Entity;
            
            CreateMesh(item.Entity);
        }
    }

    public void Update(Single deltaTime)
    {
    }
}