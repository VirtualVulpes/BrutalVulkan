using BrutalVulkan.Materials;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.ECS.Systems;

public sealed class RenderSystem : ISystem
{
    private readonly World _world;

    private RenderObject[] _renderObjects = [];
    private Int32 _renderObjectCount;

    public RenderSystem(World world)
    {
        _world = world;
    }

    public ReadOnlySpan<RenderObject> BuildRenderObjects()
    {
        var transforms = _world.GetPool<Transform>();
        var meshIds = _world.GetPool<MeshId>();
        var materialIds = _world.GetPool<MaterialId>();

        EnsureCapacity(transforms.Count);

        _renderObjectCount = 0;
        
        for (var i = 0; i < transforms.Count; i++)
        {
            var entity = transforms.GetEntityAt(i);

            if (!meshIds.Has(entity) || !materialIds.Has(entity)) continue;
            
            _renderObjects[_renderObjectCount++] = new RenderObject
            {
                ModelMatrix = transforms.Get(entity).GetModelMatrix(),
                MeshId = meshIds.Get(entity),
                MaterialId = materialIds.Get(entity)
            };
        }

        return _renderObjects.AsSpan(0, _renderObjectCount);
    }

    private void EnsureCapacity(Int32 required)
    {
        if (_renderObjects.Length >= required) return;
        
        Array.Resize(ref _renderObjects, Int32.Max(required, 16));
    }

    public void Update(Single deltaTime)
    {
    }
}