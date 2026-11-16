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
        var query = _world.Query<Transform, MaterialId, MeshId>();
        
        EnsureCapacity(query.MaxCount);

        _renderObjectCount = 0;
        
        foreach (var item in query)
        {
            _renderObjects[_renderObjectCount++] = new RenderObject
            {
                ModelMatrix = item.C1.GetModelMatrix(),
                MaterialId = item.C2,
                MeshId = item.C3
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