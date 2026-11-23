using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;
using Math = BrutalVulkan.Utility.Math;

namespace BrutalVulkan;

public sealed class SceneLoader
{
    private readonly AssetResolver _assets;

    public SceneLoader(AssetResolver assets)
    {
        _assets = assets;
    }

    public void Load(SceneDef scene, World world)
    {
        foreach (var def in scene.Definitions)
        {
            switch (def)
            {
                case EntityDef entityDef:
                    LoadEntity(entityDef, world);
                    break;
                case CelestialEntityDef celestialDef:
                    LoadCelestialBody(celestialDef, world);
                    break;
            }
        }
    }

    private Entity LoadEntity(EntityDef entityDef, World world)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new NameComponent
        {
            Name = entityDef.Name
        });

        foreach (var child in entityDef.Children)
        {
            var c = LoadEntity(child, world);
            world.AddComponent(c, new ParentComponent { Parent = entity });
        }

        foreach (var component in entityDef.Components)
        {
            component.Apply(world, entity, _assets);
        }

        return entity;
    }

    private void LoadCelestialBody(CelestialEntityDef celestialEntityDef, World world)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new NameComponent
        {
            Name = celestialEntityDef.Name
        });
        
        foreach (var child in celestialEntityDef.Children)
        {
            var c = LoadEntity(child, world);
            world.AddComponent(c, new ParentComponent { Parent = entity });
        }

        foreach (var component in celestialEntityDef.Components)
        {
            component.Apply(world, entity, _assets);
        }

        var pos = world.HasComponent<CelestialBodyComponent>(entity)
            ? world.GetComponent<CelestialBodyComponent>(entity).PositionRelParent
            : throw new InvalidOperationException("Celestial body must have a celestial body component");

        foreach (var face in Enum.GetValues<Face>())
        {
            var child = world.CreateEntity();
            world.AddComponent(child, new NodeComponent { Face = face, Level = 0, X = 0, Y = 0 });
            world.AddComponent(child, new ParentComponent { Parent = entity });

            var facePos = CubeSphere.GetNodePosition(face, 0, 0, 0, pos);
            
            world.AddComponent(child, new TransformComponent() { Position = Math.ToFloat3(facePos), Rotation = floatQuat.Identity, Scale = float3.One });
            foreach (var component in celestialEntityDef.FaceComponents)
            {
                component.Apply(world, child, _assets);
            }
        }
    }
}