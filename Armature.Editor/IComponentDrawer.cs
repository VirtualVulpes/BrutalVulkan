using Armature.Scene;

namespace Armature.Editor;

public interface IComponentDrawer
{
    public void Draw(World world, Entity entity);
}