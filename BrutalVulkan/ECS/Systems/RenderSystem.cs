using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.GpuTypes;
using BrutalVulkan.Utility;

namespace BrutalVulkan.ECS.Systems;

public sealed class RenderSystem : ISystem
{
    private readonly World _world;
    private readonly TransformSystem _transformSystem;

    public SceneData Scene { get; } = new();

    public RenderSystem(World world, TransformSystem transformSystem)
    {
        _world = world;
        _transformSystem = transformSystem;
    }

    public void BuildScene(GpuCamera camera)
    {
        Scene.Camera = camera;
        
        BuildRenderObjects();
        BuildGpuLights();
        BuildAtmosphere();
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
                ModelMatrix = _transformSystem.GetWorldMatrix(item.Entity),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                Color = item.C4.Color,
                DataType = RenderDataType.Color,
                IsSelected = isSelected,
                Entity = item.Entity
            };
        }

        foreach (var item in textureQuery)
        {
            var isSelected = _world.HasComponent<SelectionComponent>(item.Entity);

            Scene.Objects[Scene.ObjectCount++] = new RenderObject
            {
                ModelMatrix = _transformSystem.GetWorldMatrix(item.Entity),
                PipelineId = item.C2.PipelineId,
                MeshId = item.C3.MeshId,
                TextureId = item.C4.TextureId,
                DataType = RenderDataType.Texture,
                IsSelected = isSelected,
                Entity = item.Entity
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
            var world = _transformSystem.GetWorldMatrix(light.Entity);
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

    private void BuildAtmosphere()
    {
        Scene.Atmosphere = default;
        
        foreach (var atmo in _world.Query<AtmosphereComponent, TransformComponent>())
        {
            var atmoWorld = _transformSystem.GetWorldMatrix(atmo.Entity);
            var scale = TransformUtility.GetScale(atmoWorld);
            
            foreach (var light in _world.Query<LightComponent, TransformComponent>())
            {
                if (light.C1.Type != LightType.Directional) continue;
                
                Scene.Atmosphere = new AtmosphereRenderData
                {
                    Center = atmoWorld.Translation,
                    Radius = atmo.C1.Radius * Single.Max(scale.X, Single.Max(scale.Y, scale.Z)),
                    LightDirection = GetLightDirection(light.Entity, light.C1.Direction),
                    Enabled = 1
                };
                return; // exits after finding first light (the sun)
            }
            return; // exits if no sun
        }
    }

    private float3 GetLightDirection(Entity e, float3 localDir)
    {
        var world = _transformSystem.GetWorldMatrix(e);
        return float3.NormalizeOrZero(float3.TransformNormal(localDir, world));
    }
    
    private float3 GetLightDirection(float4x4 world, float3 localDir)
    {
        return float3.NormalizeOrZero(float3.TransformNormal(localDir, world));
    }


    public void Start()
    {
    }

    public void Update(Single deltaTime)
    {
    }
}