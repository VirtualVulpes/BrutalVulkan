using Brutal.Numerics;

namespace BrutalVulkan.Utility;

public static class Math
{
    public static float3 ToFloat3(double3 value)
    {
        return new float3((Single)value.X, (Single)value.Y, (Single)value.Z);
    }
    
    public static double3 ToDouble3(float3 value)
    {
        return new double3(value.X, value.Y, value.Z);
    }
}