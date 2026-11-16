using Brutal.Numerics;

namespace BrutalVulkan;

public static class CubeSphere
{
    private static readonly float3[] Origins =
    [
        new(-0.5f, -0.5f, 0.5f), // front
        new(0.5f, -0.5f, 0.5f), // right
        new(0.5f, -0.5f, -0.5f), // back
        new(-0.5f, -0.5f, -0.5f), // left
        new(-0.5f, 0.5f, 0.5f), // top
        new(-0.5f, -0.5f, -0.5f) // bottom
    ];

    private static readonly float3[] UDirections =
    [
        new(1.0f, 0.0f, 0.0f), // front
        new(0.0f, 0.0f, -1.0f), // right
        new(-1.0f, 0.0f, 0.0f), // back
        new(0.0f, 0.0f, 1.0f), // left
        new(1.0f, 0.0f, 0.0f), // top
        new(1.0f, 0.0f, 0.0f) // bottom
    ];

    private static readonly float3[] VDirections =
    [
        new(0.0f, 1.0f, 0.0f), // front
        new(0.0f, 1.0f, 0.0f), // right
        new(0.0f, 1.0f, 0.0f), // back
        new(0.0f, 1.0f, 0.0f), // left
        new(0.0f, 0.0f, -1.0f), // top
        new(0.0f, 0.0f, 1.0f) // bottom
    ];

    private static readonly FastNoiseLite Noise = new();

    public static void SetNoiseType(FastNoiseLite.NoiseType type)
    {
        Noise.SetNoiseType(type);
    }

    private static Single SampleNoise(float3 position)
    {
        var frequency = 0.5f;
        var amplitude = 1.0f;

        var total = 0.0f;
        var maxValue = 0.0f;

        Noise.SetFrequency(frequency);

        for (var i = 0; i < 5; i++)
        {
            total += Noise.GetNoise(position.X, position.Y, position.Z) * amplitude;
            maxValue += amplitude;

            frequency *= 2.0f;
            amplitude *= 0.5f;
        }

        return total / maxValue;
    }

    public static Mesh CreateMesh(Int32 subdivisionLevel, Single radius = 1.0f)
    {
        var rawMesh = GenerateRawMesh(subdivisionLevel);

        var vertices = rawMesh.Vertices.ToArray();

        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i].Position *= radius;
        }

        return new Mesh(vertices, [.. rawMesh.Indices]);
    }

    public static Mesh CreateNoiseMesh(Int32 subdivisionLevel, Single radius = 1.0f)
    {
        var rawMesh = GenerateRawMesh(subdivisionLevel);

        var vertices = rawMesh.Vertices.ToArray();


        for (var i = 0; i < vertices.Length; i++)
        {
            var direction = vertices[i].Position;
            var displacement = SampleNoise(direction) * 0.2f;

            vertices[i].Position = direction * radius * (1.0f + displacement);
        }

        return new Mesh(vertices, [.. rawMesh.Indices]);
    }

    private static Mesh GenerateRawMesh(Int32 subdivisionLevel)
    {
        var verticesPerSide = (1 << subdivisionLevel) + 1;
        var verticesPerFace = verticesPerSide * verticesPerSide;
        var vertices = new Vertex[verticesPerFace * 6];

        var squaresPerSide = verticesPerSide - 1;

        var indicesPerFace = squaresPerSide * squaresPerSide * 6;
        var indices = new UInt32[indicesPerFace * 6];

        Int32[] baseIndices = [0, 1, verticesPerSide + 1, verticesPerSide + 1, verticesPerSide, 0];

        for (var f = 0; f < 6; f++)
        {
            for (var v = 0; v < verticesPerSide; v++)
            {
                var vOffset = (Single)v / (verticesPerSide - 1);

                for (var u = 0; u < verticesPerSide; u++)
                {
                    var uOffset = (Single)u / (verticesPerSide - 1);

                    var direction =
                        float3.NormalizeOrZero(Origins[f] + uOffset * UDirections[f] + vOffset * VDirections[f]);

                    vertices[u + v * verticesPerSide + f * verticesPerFace] = new Vertex
                    {
                        Position = direction,
                        Normal = direction,
                        Uv = new float2(uOffset, 1.0f - vOffset)
                    };
                }
            }
        }

        for (var f = 0; f < 6; f++)
        {
            for (var v = 0; v < squaresPerSide; v++)
            {
                for (var u = 0; u < squaresPerSide; u++)
                {
                    for (var i = 0; i < 6; i++)
                    {
                        indices[i + u * 6 + v * 6 * squaresPerSide + f * indicesPerFace] =
                            (UInt32)(baseIndices[i] + u + v * verticesPerSide + f * verticesPerFace);
                    }
                }
            }
        }

        return new Mesh(vertices, indices);
    }
}