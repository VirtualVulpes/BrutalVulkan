using Brutal.Numerics;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.ECS;

public interface ISceneDef;

public sealed record EntityDef(String Name, IComponentDef[] Components, EntityDef[]? Children = null) : ISceneDef
{
    public EntityDef[] Children { get; } = Children ?? [];
}

public sealed record CelestialEntityDef(
    String Name,
    Double Radius,
    IComponentDef[] Components,
    IComponentDef[] FaceComponents,
    EntityDef[]? Children = null) : ISceneDef
{
    public EntityDef[] Children { get; } = Children ?? [];
}

public sealed record SceneDef(String Name, IReadOnlyList<ISceneDef> Definitions);

public interface IComponentDef
{
    void Apply(World world, Entity entity, AssetResolver assets);
}

public sealed record TransformDef : IComponentDef
{
    public float3 Position = float3.Zero;
    public floatQuat Rotation = floatQuat.Identity;
    public float3 Scale = float3.One;

    public static TransformDef Identity()
    {
        return new TransformDef
        {
            Position = float3.Zero,
            Rotation = floatQuat.Identity,
            Scale = float3.One
        };
    }

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

public sealed record RotateDef : IComponentDef
{
    public float3 LocalAngularVelocity = float3.Zero;
    public float3 WorldAngularVelocity = float3.Zero;

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

public sealed record LightDef : IComponentDef
{
    public LightType Type;
    public float3 Color;
    public Single Intensity;
    public Single Range = 0.0f;
    public float3 Direction = float3.Zero;
    public Single InnerCos = 0.0f;
    public Single OuterCos = 0.0f;

    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity,
            new LightComponent
            {
                Color = Color, Intensity = Intensity, Range = Range, Direction = Direction, Type = Type,
                InnerCos = InnerCos, OuterCos = OuterCos
            });
    }
}

public sealed record CameraDef : IComponentDef
{
    public Single AspectRatio = (Single)Constants.WindowWidth / Constants.WindowHeight;
    public Single Fov = Single.DegreesToRadians(60.0f);
    public Single ZNear = 0.1f;
    public Single ZFar = 100.0f;

    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity,
            new CameraComponent { AspectRatio = AspectRatio, Fov = Fov, ZNear = ZNear, ZFar = ZFar });
    }
}

public sealed record AtmosphereDef(Single Radius) : IComponentDef
{
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity, new AtmosphereComponent { Radius = Radius });
    }
}

public sealed record CelestialBodyDef : IComponentDef
{
    public double3 PositionRelParent;
    public double3 VelocityRelParent;
    public doubleQuat Rotation;
    
    public void Apply(World world, Entity entity, AssetResolver assets)
    {
        world.AddComponent(entity,
            new CelestialBodyComponent
            {
                PositionRelParent = PositionRelParent, VelocityRelParent = VelocityRelParent,
                Rotation = Rotation
            });
    }
}