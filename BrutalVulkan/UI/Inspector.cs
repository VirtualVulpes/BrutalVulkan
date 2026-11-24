using Armature.Scene.Components;
using Brutal.ImGuiApi;
using Armature.Scene;

namespace BrutalVulkan.UI;

public sealed class Inspector
{
    private readonly World _world;
    private readonly SelectionHelper _selection;

    private readonly Dictionary<Type, IComponentDrawer> _drawers = new();

    public Inspector(World world, SelectionHelper selection)
    {
        _world = world;
        _selection = selection;

        Register<NameComponent>("Name", DrawName);
        Register<TransformComponent>("Transform", DrawTransform);
        Register<RotateComponent>("Rotation", DrawRotate);
        Register<ColorComponent>("Color", DrawColor);
        Register<LightComponent>("Light", DrawLight);
        Register<CameraComponent>("Camera", DrawCamera);
        Register<MeshComponent>("Mesh", DrawMesh);
        Register<TextureComponent>("Texture", DrawTexture);
        Register<PipelineComponent>("Pipeline", DrawPipeline);
    }

    private void Register<T>(String name, RefAction<T> draw) where T : struct
    {
        _drawers[typeof(T)] = new ComponentDrawer<T>(name, draw);
    }

    public void Draw()
    {
        var e = _selection.GetActive();
        if (!_world.IsEntityAlive(e)) return;
        
        ImGui.Begin("Inspector"u8, ImGuiWindowFlags.AlwaysAutoResize);

        ImGui.PushID((Int32)e.Id.Value);
        foreach (var drawer in _drawers.Values)
        {
            drawer.Draw(_world, e);
        }
        ImGui.PopID();
        
        ImGui.End();
    }

    private static Boolean EnumCombo<T>(String label, ref T value) where T : struct, Enum
    {
        var changed = false;

        if (ImGui.BeginCombo(label, value.ToString()))
        {
            foreach (var item in Enum.GetValues<T>())
            {
                var selected = EqualityComparer<T>.Default.Equals(value, item);

                if (ImGui.Selectable(item.ToString(), selected))
                {
                    value = item;
                    changed = true;
                }
                
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            
            ImGui.EndCombo();
        }

        return changed;
    }
    
    private void DrawName(ref NameComponent c)
    {
        ImGui.Text(c.Name);
    }
    
    private void DrawTransform(ref TransformComponent c)
    {
        ImGui.DragFloat3("Position"u8, ref c.Position);
        ImGui.DragFloat3("Scale"u8, ref c.Scale);
    }

    private void DrawRotate(ref RotateComponent c)
    {
        ImGui.DragFloat3("Local"u8, ref c.LocalAngularVelocity);
        ImGui.DragFloat3("World"u8, ref c.WorldAngularVelocity);
    }
    
    private void DrawColor(ref ColorComponent c)
    {
        ImGui.DragFloat3("Color"u8, ref c.Color);
    }
    
    private void DrawLight(ref LightComponent c)
    {
        EnumCombo("Type", ref c.Type);
        
        ImGui.DragFloat3("Color"u8, ref c.Color);
        ImGui.DragFloat("Intensity"u8, ref c.Intensity);

        switch (c.Type)
        {
            case LightType.Point:
                ImGui.DragFloat("Range"u8, ref c.Range);
                break;
            case LightType.Directional:
                ImGui.DragFloat3("Direction"u8, ref c.Direction);
                break;
            case LightType.Spot:
                ImGui.DragFloat("Range"u8, ref c.Range);
                ImGui.DragFloat3("Direction"u8, ref c.Direction);
                ImGui.DragFloat("Inner Cos"u8, ref c.InnerCos);
                ImGui.DragFloat("Outer Cos"u8, ref c.OuterCos);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid light type: {c.Type}");
        }
    }
    
    private void DrawCamera(ref CameraComponent c)
    {
        ImGui.Text($"Aspect Ratio {c.AspectRatio}");
        ImGui.Text($"FOV {Single.RadiansToDegrees(c.Fov)}");
        ImGui.Text($"Z Near {c.ZNear}");
        ImGui.Text($"Z Far {c.ZFar}");
    }
    
    private void DrawMesh(ref MeshComponent c)
    {   
        ImGui.Text($"Id {c.MeshId.Value}");
    }
    
    private void DrawTexture(ref TextureComponent c)
    {   
        ImGui.Text($"Id {c.TextureId.Value}");
    }
    
    private void DrawPipeline(ref PipelineComponent c)
    {   
        ImGui.Text($"Id {c.PipelineId.Value}");
    }
}