using Brutal.Numerics;

namespace Armature.Core;

public static class MathEx
{
    public static float3 ToFloat3(double3 value)
    {
        return new float3((float)value.X, (float)value.Y, (float)value.Z);
    }
    
    public static double3 ToDouble3(float3 value)
    {
        return new double3(value.X, value.Y, value.Z);
    }
}