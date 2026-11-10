using Brutal.Numerics;

namespace BrutalVulkan;

public static class CubeSphere
{
    private static readonly FastNoiseLite Noise = new();

    public static void SetNoiseType(FastNoiseLite.NoiseType type)
    {
        Noise.SetNoiseType(type);
    }
    
    private static float SampleNoise(float3 position)
    {
        float frequency = 0.5f;
        float amplitude = 1.0f;

        float total = 0.0f;
        float maxValue = 0.0f;

        for (int i = 0; i < 5; i++)
        {
            Noise.SetFrequency(frequency);

            total += Noise.GetNoise(position.X, position.Y, position.Z) * amplitude;
            maxValue += amplitude;

            frequency *= 2.0f;
            amplitude *= 0.5f;
        }

        return total / maxValue;
    }
    
    public static Mesh CreateMesh(int subdivisionLevel, float radius = 1.0f)
    {
        float3[] origins =
        [
            new(-0.5f, -0.5f, 0.5f), // front
            new(0.5f, -0.5f, 0.5f), // right
            new(0.5f, -0.5f, -0.5f), // back
            new(-0.5f, -0.5f, -0.5f), // left
            new(-0.5f, 0.5f, 0.5f), // top
            new(-0.5f, -0.5f, -0.5f) // bottom
        ];

        float3[] uDirections =
        [
            new(1.0f, 0.0f, 0.0f), // front
            new(0.0f, 0.0f, -1.0f), // right
            new(-1.0f, 0.0f, 0.0f), // back
            new(0.0f, 0.0f, 1.0f), // left
            new(1.0f, 0.0f, 0.0f), // top
            new(1.0f, 0.0f, 0.0f) // bottom
        ];

        float3[] vDirections =
        [
            new(0.0f, 1.0f, 0.0f), // front
            new(0.0f, 1.0f, 0.0f), // right
            new(0.0f, 1.0f, 0.0f), // back
            new(0.0f, 1.0f, 0.0f), // left
            new(0.0f, 0.0f, -1.0f), // top
            new(0.0f, 0.0f, 1.0f) // bottom
        ];

        var verticesPerSide = (1 << subdivisionLevel) + 1;
        var verticesPerFace = verticesPerSide * verticesPerSide;
        var vertices = new Vertex[verticesPerFace * 6];

        var squaresPerSide = verticesPerSide - 1;

        var indicesPerFace = squaresPerSide * squaresPerSide * 6;
        var indices = new UInt32[indicesPerFace * 6];

        int[] baseIndices = [0, 1, verticesPerSide + 1, verticesPerSide + 1, verticesPerSide, 0];

        for (int f = 0; f < 6; f++)
        {
            for (int v = 0; v < verticesPerSide; v++)
            {
                var vOffset = (float)v / (verticesPerSide - 1);

                for (int u = 0; u < verticesPerSide; u++)
                {
                    var uOffset = (float)u / (verticesPerSide - 1);

                    var direction =
                        float3.NormalizeOrZero(origins[f] + uOffset * uDirections[f] + vOffset * vDirections[f]);
                    
                    vertices[u + v * verticesPerSide + f * verticesPerFace] = new Vertex
                    {
                        Position = direction * (radius + SampleNoise(direction) * 0.2f),
                        Normal = direction,
                        Uv = new float2(uOffset, 1.0f - vOffset)
                    };
                }
            }
        }

        for (int f = 0; f < 6; f++)
        {
            for (int v = 0; v < squaresPerSide; v++)
            {
                for (int u = 0; u < squaresPerSide; u++)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        indices[i + u * 6 + v * 6 * squaresPerSide + f * indicesPerFace] =
                            (uint)(baseIndices[i] + u + v * verticesPerSide + f * verticesPerFace);
                    }
                }
            }
        }

        return new Mesh(vertices, indices);
    }
}