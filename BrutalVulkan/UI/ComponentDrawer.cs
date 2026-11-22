using Brutal.ImGuiApi;
using BrutalVulkan.ECS;

namespace BrutalVulkan.UI;

public delegate void RefAction<T>(ref T value);

public sealed class ComponentDrawer<T> : IComponentDrawer where T : struct
{
    private readonly String _name;
    private readonly RefAction<T> _draw;

    public ComponentDrawer(String name, RefAction<T> draw)
    {
        _name = name;
        _draw = draw;
    }

    public void Draw(World world, Entity entity)
    {
        if (!world.HasComponent<T>(entity)) return;

        if (ImGui.CollapsingHeader(_name, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ref var component = ref world.GetComponent<T>(entity);
            
            ImGui.PushID(typeof(T).Name);
            _draw(ref component);
            ImGui.PopID();
        }
    }
}