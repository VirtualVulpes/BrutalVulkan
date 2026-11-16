using Brutal.Numerics;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.ECS;

public sealed record EntityDef(String Name, params IComponentDef[] Components);

public sealed record SceneDef(String Name, IReadOnlyList<EntityDef> Entities);

public interface IComponentDef
{
    void Apply(World world, Entity entity, AssetResolver assets);
}

public sealed record TransformDef(float3 Position, floatQuat Rotation, float3 Scale) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new TransformComponent { Position = Position, Rotation = Rotation, Scale = Scale });
    }
}

public sealed record MeshDef(String Name) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new MeshComponent { MeshId = assets.Mesh(Name) });
    }
}

public sealed record TextureDef(String Name) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new TextureComponent { TextureId = assets.Texture(Name) });
    }
}

public sealed record PipelineDef(String Name) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new PipelineComponent { PipelineId = assets.Pipeline(Name) });
    }
}

public sealed record RotateDef(float3 LocalAngularVelocity, float3 WorldAngularVelocity) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity,
            new RotateComponent
                { LocalAngularVelocity = LocalAngularVelocity, WorldAngularVelocity = WorldAngularVelocity });
    }
}

public sealed record ColorDef(float3 Color) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new ColorComponent { Color = Color });
    }
}

public sealed record LightDef(float3 Color, Single Intensity) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new LightComponent{ Color = Color, Intensity = Intensity});
    }
}