using System.Runtime.InteropServices.ComTypes;
using Brutal.GlfwApi;
using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Utility;

namespace BrutalVulkan.ECS.Systems;

public readonly record struct CameraData(float3 Position, float4x4 View, float4x4 Projection);

public class CameraSystem : ISystem
{
    private readonly World _world;
    private readonly List<Entity> _cameras = [];

    public Entity ActiveCamera { get; private set; }

    public CameraSystem(World world)
    {
        _world = world;
    }

    public void RegisterCamera(Entity entity)
    {
        _cameras.Add(entity);
    }

    public void UnregisterCamera(Entity entity)
    {
        _cameras.Remove(entity);

        if (ActiveCamera == entity)
            throw new InvalidOperationException("Active camera cannot be unregistered");
    }

    public void SetActiveCamera(Entity entity)
    {
        if (!_cameras.Contains(entity))
            throw new ArgumentException("Camera is not registered");
        
        ActiveCamera = entity;
    }

    public void NextCamera()
    {
        if (_cameras.Count == 0) return;

        var index = _cameras.IndexOf(ActiveCamera);
        ActiveCamera = _cameras[(index + 1) % _cameras.Count];
    }

    public void PreviousCamera()
    {
        if (_cameras.Count == 0) return;

        var index = _cameras.IndexOf(ActiveCamera);
        ActiveCamera = _cameras[(index - 1 + _cameras.Count) % _cameras.Count];
    }
    
    public CameraData GetActiveCameraData()
    {
        var transform = _world.GetComponent<TransformComponent>(ActiveCamera);
        var camera = _world.GetComponent<CameraComponent>(ActiveCamera);

        return new CameraData(
            transform.Position, 
            float4x4.CreateLookAt(transform.Position, transform.Position + TransformUtility.Forward(transform), float3.UnitY),
            float4x4.CreatePerspectiveFieldOfView(camera.Fov, camera.AspectRatio, camera.ZNear, camera.ZFar));
    }

    public void SetAspectRatio(Single aspectRatio)
    {
        foreach (var entity in _cameras)
        {
            ref var camera = ref _world.GetComponent<CameraComponent>(entity);
            camera.AspectRatio = aspectRatio;
        }
    }
    
    public void Update(Single deltaTime)
    {
        
    }
}