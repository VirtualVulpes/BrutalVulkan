using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Rendering;

namespace BrutalVulkan.ECS.Systems;

public sealed class RenderSystem : ISystem
{
    private readonly World _world;

    private RenderObject[] _renderObjects = [];
    private Int32 _renderObjectCount;
    
    private GpuLight[] _gpuLights = [];
    private Int32 _gpuLightCount;

    public RenderSystem(World world)
    {
        _world = world;
    }

    public ReadOnlySpan<RenderObject> BuildRenderObjects()
    {
        var colorQuery = _world.Query<TransformComponent, PipelineComponent, MeshComponent, ColorComponent>();
        var textureQuery = _world.Query<TransformComponent, PipelineComponent, MeshComponent, TextureComponent>();
        
        if (_renderObjects.Length < colorQuery.MaxCount + textureQuery.MaxCount)
            Array.Resize(ref _renderObjects, Int32.Max(colorQuery.MaxCount + textureQuery.MaxCount, 16));
        
        _renderObjectCount = 0;
        
        foreach (var item in colorQuery)
        {
            _renderObjects[_renderObjectCount++] = new RenderObject
            {
                ModelMatrix = item.C1.GetModelMatrix(),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                Color = item.C4.Color,
                DataType = RenderDataType.Color
            };
        }
        
        foreach (var item in textureQuery)
        {
            _renderObjects[_renderObjectCount++] = new RenderObject
            {
                ModelMatrix = item.C1.GetModelMatrix(),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                TextureId = item.C4.TextureId,
                DataType = RenderDataType.Texture
            };
        }
        
        return _renderObjects.AsSpan(0, _renderObjectCount);
    }

    public ReadOnlySpan<GpuLight> BuildGpuLights()
    {
        var query = _world.Query<TransformComponent, LightComponent>();
        
        if (_gpuLights.Length < query.MaxCount)
            Array.Resize(ref _gpuLights, Int32.Max(query.MaxCount, 16));
        
        _gpuLightCount = 0;
        
        foreach (var item in query)
        {
            _gpuLights[_gpuLightCount++] = new GpuLight
            {
                Position = new float4(item.C1.Position, 0.0f),
                Color = new float4(item.C2.Color, 0.0f),
                Intensity = item.C2.Intensity
            };
        }
        
        return _gpuLights.AsSpan(0, _gpuLightCount);
    }

    public void Update(Single deltaTime)
    {
    }
}