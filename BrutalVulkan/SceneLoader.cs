using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;

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
                case PlanetDef planetDef:
                    LoadPlanet(planetDef, world);
                    break;
            }
        }
    }

    private void LoadEntity(EntityDef entityDef, World world)
    {
        var entity = world.CreateEntity();

        foreach (var component in entityDef.Components)
        {
            component.Apply(world, entity, _assets);
        }
    }

    private void LoadPlanet(PlanetDef planetDef, World world)
    {
        var entity = world.CreateEntity();
        
        foreach (var component in planetDef.Components)
        {
            component.Apply(world, entity, _assets);
        }
        
        foreach (var face in Enum.GetValues<Face>())
        {
            var child = world.CreateEntity();
            world.AddComponent(child, new NodeComponent { Face = face, Level = 0, X = 0, Y = 0 });
            world.AddComponent(child, new ParentComponent {Parent = entity});
            foreach (var component in planetDef.ChildComponents)
            {
                component.Apply(world, child, _assets);
            }
        }
    }
}