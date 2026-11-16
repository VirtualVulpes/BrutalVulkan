using Brutal.Numerics;

namespace BrutalVulkan;

public class Camera
{
    private const Single ZNear = 0.1f;
    private const Single ZFar = 100.0f;

    private readonly Single _fov = Single.DegreesToRadians(60.0f);

    private Single _yaw = Single.DegreesToRadians(-90.0f);
    private Single _pitch;
    private float3 _forward = new(0.0f, 0.0f, -1.0f);
    private float3 _right = float3.UnitX;

    public float3 Position { get; private set; } = new(0.0f, 1.0f, 3.0f);
    public Single AspectRatio { get; set; } = (Single)Constants.WindowWidth / Constants.WindowHeight;

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

    public void Rotate(Single yawDelta, Single pitchDelta)
    {
        _yaw += yawDelta;
        _pitch = Single.Clamp(_pitch + pitchDelta, Single.DegreesToRadians(-89.0f), Single.DegreesToRadians(89.0f));
        UpdateVectors();
    }

    private void UpdateVectors()
    {
        _forward = float3.NormalizeOrZero(new float3(Single.Cos(_yaw) * Single.Cos(_pitch), Single.Sin(_pitch),
            Single.Sin(_yaw) * Single.Cos(_pitch)));
        _right = float3.NormalizeOrZero(float3.Cross(_forward, float3.UnitY));
    }
}