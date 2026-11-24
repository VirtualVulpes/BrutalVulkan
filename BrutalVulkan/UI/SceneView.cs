using Armature.Input;
using Armature.Scene;
using Armature.Scene.Components;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace BrutalVulkan.UI;

public sealed class SceneView
{
    private readonly World _world;
    private readonly SelectionHelper _selection;
    private readonly InputManager _input;

    private readonly Dictionary<Entity, List<Entity>> _children = new();
    private readonly List<Entity> _roots = [];
    private Entity _active;
    private Entity? _clicked;
    private (Entity child, Entity parent, Boolean has) _pendingReparent;

    public SceneView(World world, SelectionHelper selection, InputManager input)
    {
        _world = world;
        _selection = selection;
        _input = input;
    }

    public void Draw()
    {
        _active = _selection.GetActive();
        _clicked = null;
        _pendingReparent = default;

        BuildHierarchy();

        ImGui.Begin("Scene"u8, ImGuiWindowFlags.AlwaysAutoResize);

        foreach (var root in _roots)
        {
            DrawNode(root);
        }

        ImGui.End();
        
        if (_clicked is { } c)
            _selection.Click(c, _input.IsKeyDown(GlfwKey.LeftShift));
        
        if (_pendingReparent.has)
            HierarchyExtensions.AddParent(_world, _pendingReparent.child, _pendingReparent.parent);
    }

    private void BuildHierarchy()
    {
        _children.Clear();
        _roots.Clear();

        foreach (var item in _world.Query<ParentComponent>())
        {
            var parent = item.C1.Parent;
            if (!_children.TryGetValue(parent, out var list))
                _children[parent] = list = [];
            list.Add(item.Entity);
        }

        foreach (var entity in _world.AliveEntities)
        {
            if (!_world.HasComponent<ParentComponent>(entity))
                _roots.Add(entity);
        }
    }

    private void DrawNode(Entity entity)
    {
        _children.TryGetValue(entity, out var kids);
        var hasKids = kids is { Count: > 0 };

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen;
        if (!hasKids) flags |= ImGuiTreeNodeFlags.Leaf;
        if (_world.HasComponent<SelectionComponent>(entity)) flags |= ImGuiTreeNodeFlags.Selected;

        var name = GetName(entity);

        ImGui.PushID((Int32)entity.Id.Value);

        var isActive = entity == _active;
        if (isActive) ImGui.PushStyleColor(ImGuiCol.Header, new float4(0.18f, 0.18f, 0.18f, 1.0f));
        
        var open = ImGui.TreeNodeEx(name, flags);

        if (isActive)
            ImGui.PopStyleColor();

        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            _clicked = entity;

        if (open)
        {
            if (hasKids)
                foreach (var child in kids!)
                    DrawNode(child);

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private String GetName(Entity entity)
    {
        return _world.HasComponent<NameComponent>(entity)
            ? _world.GetComponent<NameComponent>(entity).Name
            : $"Entity {entity.Id.Value}";
    }
}