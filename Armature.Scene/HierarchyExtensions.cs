using Armature.Scene.Components;

namespace Armature.Scene;

public static class HierarchyExtensions
{
    public static void AddParent(World world, Entity child, Entity parent)
    {
        world.AddComponent(child, new ParentComponent(parent));
    }
}