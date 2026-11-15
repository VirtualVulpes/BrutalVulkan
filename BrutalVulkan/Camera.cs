using Brutal.Numerics;

namespace BrutalVulkan;

public class Camera
{
    private const float ZNear = 0.1f;
    private const float ZFar = 100.0f;

    private readonly float _fov = float.DegreesToRadians(60.0f);

    private float _yaw = float.DegreesToRadians(-90.0f);
    private float _pitch;
    private float3 _forward = new(0.0f, 0.0f, -1.0f);
    private float3 _right = float3.UnitX;

    public float3 Position { get; set; } = new(0.0f, 1.0f, 3.0f);
    public float AspectRatio { get; set; } = (float)Constants.WindowWidth / Constants.WindowHeight;

    public float4x4 GetView()
    {
        return float4x4.CreateLookAt(Position, Position + _forward, float3.UnitY);
    }

    public float4x4 GetProjection()
    {
        return float4x4.CreatePerspectiveFieldOfView(_fov, AspectRatio, ZNear, ZFar);
    }

    public void MoveRelative(float3 localDelta)
    {
        Position += _right * localDelta.X + float3.UnitY * localDelta.Y + _forward * localDelta.Z;
    }

    public void Rotate(float yawDelta, float pitchDelta)
    {
        _yaw += yawDelta;
        _pitch = float.Clamp(_pitch + pitchDelta, float.DegreesToRadians(-89.0f), float.DegreesToRadians(89.0f));
        UpdateVectors();
    }

    private void UpdateVectors()
    {
        _forward = float3.NormalizeOrZero(new float3(float.Cos(_yaw) * float.Cos(_pitch), float.Sin(_pitch),
            float.Sin(_yaw) * float.Cos(_pitch)));
        _right = float3.NormalizeOrZero(float3.Cross(_forward, float3.UnitY));
    }
}