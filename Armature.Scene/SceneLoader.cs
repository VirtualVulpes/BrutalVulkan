using Armature.Core;
using Armature.Scene.Components;
using Brutal.Numerics;

namespace Armature.Scene;

public sealed class SceneLoader
{
    private readonly IAssetResolver _assets;

    public SceneLoader(IAssetResolver assets)
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
}