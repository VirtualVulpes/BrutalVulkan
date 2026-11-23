using Brutal.Numerics;
using BrutalVulkan.ECS.Components;
using BrutalVulkan.Rendering.Meshes;

namespace BrutalVulkan;

public static class CubeSphere
{
    private static readonly double3[] Origins =
    [
        new(-1.0f, -1.0f, 1.0f), // front
        new(1.0f, -1.0f, 1.0f), // right
        new(1.0f, -1.0f, -1.0f), // back
        new(-1.0f, -1.0f, -1.0f), // left
        new(-1.0f, 1.0f, 1.0f), // top
        new(-1.0f, -1.0f, -1.0f) // bottom
    ];

    private static readonly double3[] UDirections =
    [
        new(1.0f, 0.0f, 0.0f), // front
        new(0.0f, 0.0f, -1.0f), // right
        new(-1.0f, 0.0f, 0.0f), // back
        new(0.0f, 0.0f, 1.0f), // left
        new(1.0f, 0.0f, 0.0f), // top
        new(1.0f, 0.0f, 0.0f) // bottom
    ];

    private static readonly double3[] VDirections =
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
        var rawMesh = GenerateUnitSphere(subdivisionLevel);

        var vertices = rawMesh.Vertices.ToArray();

        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i].Position *= radius;
        }

        return new Mesh(vertices, [.. rawMesh.Indices]);
    }

    public static Mesh CreateNoiseMesh(Int32 subdivisionLevel, Single radius = 1.0f)
    {
        var rawMesh = GenerateUnitSphere(subdivisionLevel);

        var vertices = rawMesh.Vertices.ToArray();


        for (var i = 0; i < vertices.Length; i++)
        {
            var direction = vertices[i].Position;
            var displacement = SampleNoise(direction) * 0.2f;

            vertices[i].Position = direction * radius * (1.0f + displacement);
        }

        return new Mesh(vertices, [.. rawMesh.Indices]);
    }

    private static Mesh GenerateUnitSphere(Int32 subdivisionLevel)
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
                var vOffset = (Double)v / (verticesPerSide - 1);

                for (var u = 0; u < verticesPerSide; u++)
                {
                    var uOffset = (Double)u / (verticesPerSide - 1);

                    var cubePos = Origins[f] + 2 * uOffset * UDirections[f] + 2 * vOffset * VDirections[f];

                    var xSq = cubePos.X * cubePos.X;
                    var ySq = cubePos.Y * cubePos.Y;
                    var zSq = cubePos.Z * cubePos.Z;

                    var spherePos = new double3(
                        cubePos.X * Double.Sqrt(1 - (ySq / 2) - (zSq / 2) + (ySq * zSq / 3)),
                        cubePos.Y * Double.Sqrt(1 - (zSq / 2) - (xSq / 2) + (zSq * xSq / 3)),
                        cubePos.Z * Double.Sqrt(1 - (xSq / 2) - (ySq / 2) + (xSq * ySq / 3))
                    );

                    var floatPos = new float3((Single)spherePos.X, (Single)spherePos.Y, (Single)spherePos.Z);

                    vertices[u + v * verticesPerSide + f * verticesPerFace] = new Vertex
                    {
                        Position = floatPos,
                        Normal = floatPos,
                        Uv = new float2((Single)uOffset, 1.0f - (Single)vOffset)
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

    public static Mesh CreateChunkMesh(Face face, UInt32 level, UInt32 x, UInt32 y, double3 body)
    {
        var scale = 1.0 / Double.Pow(2, level);
        
        const Int32 verticesPerSide = 33;
        var vertices = new Vertex[verticesPerSide * verticesPerSide];

        const Int32 squaresPerSide = verticesPerSide - 1;

        var indices = new UInt32[squaresPerSide * squaresPerSide * 6];

        Int32[] baseIndices = [0, 1, verticesPerSide + 1, verticesPerSide + 1, verticesPerSide, 0];

        var nodeCenter = GetNodePosition(face, level, x, y, body);
        
        for (var v = 0; v < verticesPerSide; v++)
        {
            var vOffset = (Double)v / (verticesPerSide - 1);

            for (var u = 0; u < verticesPerSide; u++)
            {
                var uOffset = (Double)u / (verticesPerSide - 1);

                var cubePos =
                    Origins[(Int32)face] +
                    2 * (x + uOffset) * UDirections[(Int32)face] * scale +
                    2 * (y + vOffset) * VDirections[(Int32)face] * scale;

                var xSq = cubePos.X * cubePos.X;
                var ySq = cubePos.Y * cubePos.Y;
                var zSq = cubePos.Z * cubePos.Z;

                var spherePos = new double3(
                    cubePos.X * Double.Sqrt(1 - (ySq / 2) - (zSq / 2) + (ySq * zSq / 3)),
                    cubePos.Y * Double.Sqrt(1 - (zSq / 2) - (xSq / 2) + (zSq * xSq / 3)),
                    cubePos.Z * Double.Sqrt(1 - (xSq / 2) - (ySq / 2) + (xSq * ySq / 3))
                );

                spherePos -= nodeCenter;
                
                var floatPos = new float3((Single)spherePos.X, (Single)spherePos.Y, (Single)spherePos.Z);

                vertices[u + v * verticesPerSide] = new Vertex
                {
                    Position = floatPos,
                    Normal = floatPos,
                    Uv = new float2((Single)uOffset, 1.0f - (Single)vOffset)
                };
            }
        }

        for (var v = 0; v < squaresPerSide; v++)
        {
            for (var u = 0; u < squaresPerSide; u++)
            {
                for (var i = 0; i < 6; i++)
                {
                    indices[i + u * 6 + v * 6 * squaresPerSide] =
                        (UInt32)(baseIndices[i] + u + v * verticesPerSide);
                }
            }
        }

        return new Mesh(vertices, indices);
    }

    public static double3 GetNodePosition(Face face, UInt32 level, UInt32 x, UInt32 y, double3 body)
    {
        var scale = 1.0 / Double.Pow(2, level);
        var cubePos = Origins[(Int32)face] + 2 * (x + 0.5) * scale * UDirections[(Int32)face] + 2 * (y + 0.5) * scale * VDirections[(Int32)face];

        var xSq = cubePos.X * cubePos.X;
        var ySq = cubePos.Y * cubePos.Y;
        var zSq = cubePos.Z * cubePos.Z;
        
        var spherePos = new double3(
            cubePos.X * Double.Sqrt(1 - (ySq / 2) - (zSq / 2) + (ySq * zSq / 3)),
            cubePos.Y * Double.Sqrt(1 - (zSq / 2) - (xSq / 2) + (zSq * xSq / 3)),
            cubePos.Z * Double.Sqrt(1 - (xSq / 2) - (ySq / 2) + (xSq * ySq / 3))
        );
        
        return double3.Normalize(spherePos);
    }
}