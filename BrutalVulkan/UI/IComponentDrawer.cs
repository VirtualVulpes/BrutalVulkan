using BrutalVulkan.ECS;

namespace BrutalVulkan.UI;

public interface IComponentDrawer
{
    public void Draw(World world, Entity entity);
}