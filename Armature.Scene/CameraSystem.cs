using Armature.Scene.Components;
using Armature.Scene.Systems;
using Brutal.Numerics;

namespace Armature.Scene;

public class CameraSystem : ISystem
{
    private readonly World _world;
    private Entity _active = Entity.Invalid;

    public CameraSystem(World world)
    {
        _world = world;
    }

    public Entity ActiveCamera
    {
        get
        {
            Resolve();
            return _active;
        }
    }
    
    public void SetActiveCamera(Entity entity)
    {
        if (!_world.GetPool<CameraComponent>().Has(entity))
            throw new ArgumentException("Entity has no CameraComponent");
        _active = entity;
    }

    public void NextCamera() => Step(1);
    public void PreviousCamera() => Step(-1);

    private void Step(Int32 direction)
    {
        Resolve();
        var pool = _world.GetPool<CameraComponent>();
        if (pool.Count == 0) return;
        pool.TryGetIndex(_active, out var index);
        _active = pool.GetEntityAt((index + direction + pool.Count) % pool.Count);
    }

    public CameraData GetActiveCameraData(float2 viewportSize)
    {
        Resolve();
        if (_active == Entity.Invalid)
            throw new InvalidOperationException("No active camera");

        var transform = _world.GetComponent<TransformComponent>(ActiveCamera);
        var camera = _world.GetComponent<CameraComponent>(ActiveCamera);

        var view = float4x4.CreateLookAt(transform.Position, transform.Position + TransformExtensions.Forward(transform),
            float3.UnitY);
        var proj = float4x4.CreatePerspectiveFieldOfView(camera.Fov, camera.AspectRatio, camera.ZNear, camera.ZFar);

        return new CameraData
        {
            View = view,
            Proj = proj,
            Position = transform.Position,
            ViewportSize = viewportSize
        };
    }

    public void SetAspectRatio(Single aspectRatio)
    {
        foreach (var item in _world.Query<CameraComponent>())
        {
            ref var camera = ref item.C1;
            camera.AspectRatio = aspectRatio;
        }
    }

    public void Start()
    {
    }

    public void Update(Single deltaTime)
    {
    }
    
    private void Resolve()
    {
        var pool = _world.GetPool<CameraComponent>();
        if (_active != Entity.Invalid && pool.Has(_active)) return;

        if (pool.Count == 0)
            throw new InvalidOperationException("No camera in scene");

        _active = pool.GetEntityAt(0);
    }
}