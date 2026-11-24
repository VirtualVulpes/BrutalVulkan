using Armature.Scene;
using Armature.Scene.Components;
using Armature.Scene.Systems;
using Brutal.Numerics;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.GpuTypes;

namespace BrutalVulkan.SceneRendering;

public sealed class RenderSystem : ISystem
{
    private readonly World _world;

    public SceneData Scene { get; } = new();

    public RenderSystem(World world)
    {
        _world = world;
    }

    public void BuildScene(GpuCamera camera)
    {
        Scene.Camera = camera;
        
        BuildRenderObjects();
        BuildGpuLights();
    }

    private void BuildRenderObjects()
    {
        var colorQuery = _world.Query<TransformComponent, PipelineComponent, MeshComponent, ColorComponent>();
        var textureQuery = _world.Query<TransformComponent, PipelineComponent, MeshComponent, TextureComponent>();

        if (Scene.Objects.Length < colorQuery.MaxCount + textureQuery.MaxCount)
            Array.Resize(ref Scene.Objects, Int32.Max(colorQuery.MaxCount + textureQuery.MaxCount, 16));

        Scene.ObjectCount = 0;

        foreach (var item in colorQuery)
        {
            var isSelected = _world.HasComponent<SelectionComponent>(item.Entity);

            Scene.Objects[Scene.ObjectCount++] = new RenderObject
            {
                ModelMatrix = TransformExtensions.GetWorldMatrix(_world, item.Entity),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                Color = item.C4.Color,
                DataType = RenderDataType.Color,
                Flags = isSelected ? RenderFlags.Selected : RenderFlags.None,
                ObjectId = item.Entity.Id.Value + (item.Entity.Generation << 16)
            };
        }

        foreach (var item in textureQuery)
        {
            var isSelected = _world.HasComponent<SelectionComponent>(item.Entity);

            Scene.Objects[Scene.ObjectCount++] = new RenderObject
            {
                ModelMatrix = TransformExtensions.GetWorldMatrix(_world, item.Entity),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                TextureId = item.C4.TextureId,
                DataType = RenderDataType.Texture,
                Flags = isSelected ? RenderFlags.Selected : RenderFlags.None,
                ObjectId = item.Entity.Id.Value + (item.Entity.Generation << 16)
            };
        }
    }

    private void BuildGpuLights()
    {
        var query = _world.Query<TransformComponent, LightComponent>();

        if (Scene.Lights.Length < query.MaxCount)
            Array.Resize(ref Scene.Lights, Int32.Max(query.MaxCount, 16));

        Scene.LightCount = 0;

        foreach (var light in query)
        {
            var world = TransformExtensions.GetWorldMatrix(_world, light.Entity);
            Scene.Lights[Scene.LightCount++] = new GpuLight
            {
                Position = world.Translation,
                Color = light.C2.Color,
                Intensity = light.C2.Intensity,
                Range = light.C2.Range,
                Direction = GetLightDirection(world, light.C2.Direction),
                Type = (Int32)light.C2.Type,
                InnerCos = light.C2.InnerCos,
                OuterCos = light.C2.OuterCos
            };
        }
    }
    
    private float3 GetLightDirection(float4x4 world, float3 localDir)
    {
        return float3.NormalizeOrZero(float3.TransformNormal(localDir, world));
    }
}