using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Systems;

namespace BrutalVulkan;

public sealed class SceneLoader
{
    private readonly AssetResolver _assets;
    private readonly CameraSystem _cameraSystem;

    public SceneLoader(AssetResolver assets, CameraSystem cameraSystem)
    {
        _assets = assets;
        _cameraSystem = cameraSystem;
    }

    public void Load(SceneDef scene, World world)
    {
        foreach (var def in scene.Entities)
        {
            var entity = world.CreateEntity();
            foreach (var component in def.Components)
            {
                component.Apply(world, entity, _assets);

                if (component is not CameraDef) continue;
                
                _cameraSystem.RegisterCamera(entity);
                if (component is CameraDef { IsActive: true })
                    _cameraSystem.SetActiveCamera(entity);
            }
        }
    }
}