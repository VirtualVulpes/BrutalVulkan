using BrutalVulkan.Rendering.GpuTypes;

namespace BrutalVulkan.Rendering;

public sealed class SceneData
{
    public RenderObject[] Objects = [];
    public Int32 ObjectCount;
    public GpuLight[] Lights = [];
    public Int32 LightCount;
    public GpuCamera Camera;

    public ReadOnlySpan<RenderObject> ObjectSpan => Objects.AsSpan(0, ObjectCount);
    public ReadOnlySpan<GpuLight> LightSpan => Lights.AsSpan(0, LightCount);
}