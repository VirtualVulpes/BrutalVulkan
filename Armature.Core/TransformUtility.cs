using Brutal.Numerics;

namespace Armature.Core;

public static class TransformUtility
{
    public static float3 GetScale(float4x4 model)
    {
        var scaleX = new float3(model.X.X, model.X.Y, model.X.Z).Length();
        var scaleY = new float3(model.Y.X, model.Y.Y, model.Y.Z).Length();
        var scaleZ = new float3(model.Z.X, model.Z.Y, model.Z.Z).Length();

        return new float3(scaleX, scaleY, scaleZ);
    }

    public static (float Yaw, float Pitch) GetYawPitch(floatQuat rotation)
    {
        var forward = rotation * -float3.UnitZ;

        var yaw = float.Atan2(-forward.X, -forward.Z);
        var pitch = float.Asin(float.Clamp(forward.Y, -1.0f, 1.0f));

        return (yaw, pitch);
    }
}