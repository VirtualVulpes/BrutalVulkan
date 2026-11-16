using BrutalVulkan.ECS;

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
        foreach (var def in scene.Entities)
        {
            var entity = world.CreateEntity();
            foreach (var component in def.Components)
            {
                component.Apply(world, entity, _assets);
            }
        }
    }
}