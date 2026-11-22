using Brutal.GlfwApi;
using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.ECS.Systems;
using BrutalVulkan.Input;
using BrutalVulkan.Utility;

namespace BrutalVulkan.Camera;

public class CameraController
{
    private readonly World _world;
    private readonly InputManager _inputManager;
    private readonly CameraSystem _cameraSystem;

    private Single _yaw;
    private Single _pitch;

    private const Single Speed = 5.0f;
    private const Single Sensitivity = 0.001f;
    
    public CameraController(World world, InputManager inputManager, CameraSystem cameraSystem)
    {
        _world = world;
        _inputManager = inputManager;
        _cameraSystem = cameraSystem;
        
        SetYawPitch();
    }
    
    public void Update(Single deltaTime)
    {
        if (_inputManager.InputMode != InputMode.CameraControl) return;

        // Camera selection
        if (_inputManager.IsKeyPressed(GlfwKey.RightBracket))
        {
            _cameraSystem.NextCamera();
            SetYawPitch();
        }
        else if (_inputManager.IsKeyPressed(GlfwKey.LeftBracket))
        {
            _cameraSystem.PreviousCamera();
            SetYawPitch();
        }
        
        ref var transform = ref _world.GetComponent<TransformComponent>(_cameraSystem.ActiveCamera);

        // Camera movement
        var movement = float3.Zero;

        if (_inputManager.IsKeyDown(GlfwKey.W))
            movement += float3.UnitZ;
        if (_inputManager.IsKeyDown(GlfwKey.S))
            movement -= float3.UnitZ;
        if (_inputManager.IsKeyDown(GlfwKey.A))
            movement -= float3.UnitX;
        if (_inputManager.IsKeyDown(GlfwKey.D))
            movement += float3.UnitX;
        if (_inputManager.IsKeyDown(GlfwKey.Space))
            movement += float3.UnitY;
        if (_inputManager.IsKeyDown(GlfwKey.LeftControl))
            movement -= float3.UnitY;

        TransformUtility.MoveLocal(ref transform, float3.NormalizeOrZero(movement) * deltaTime * Speed);

        // Camera rotation
        var mouseDelta = _inputManager.CursorDelta;

        _yaw -= (Single)mouseDelta.X * Sensitivity;
        _pitch = Single.Clamp(_pitch - (Single)mouseDelta.Y * Sensitivity, Single.DegreesToRadians(-89.0f), Single.DegreesToRadians(89.0f));

        var yawRotation = floatQuat.CreateFromAxisAngle(float3.UnitY, _yaw);
        var pitchRotation = floatQuat.CreateFromAxisAngle(float3.UnitX, _pitch);

        transform.Rotation = yawRotation * pitchRotation;
    }

    private void SetYawPitch()
    {
        var transform = _world.GetComponent<TransformComponent>(_cameraSystem.ActiveCamera);
        var (yaw, pitch) = TransformUtility.GetYawPitch(transform.Rotation);
        _yaw = yaw;
        _pitch = pitch;
    }
}