using Brutal.Numerics;

namespace Armature.Rendering;

public static class CubeSphere
{
    private static readonly float3[] Origins =
    [
        new(-1.0f, -1.0f, 1.0f), // front
        new(1.0f, -1.0f, 1.0f), // right
        new(1.0f, -1.0f, -1.0f), // back
        new(-1.0f, -1.0f, -1.0f), // left
        new(-1.0f, 1.0f, 1.0f), // top
        new(-1.0f, -1.0f, -1.0f) // bottom
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

    public static Mesh CreateMesh(int subdivisionLevel, float radius = 1.0f)
    {
        var verticesPerSide = (1 << subdivisionLevel) + 1;
        var verticesPerFace = verticesPerSide * verticesPerSide;
        var vertices = new Vertex[verticesPerFace * 6];

        var squaresPerSide = verticesPerSide - 1;

        var indicesPerFace = squaresPerSide * squaresPerSide * 6;
        var indices = new uint[indicesPerFace * 6];

        int[] baseIndices = [0, 1, verticesPerSide + 1, verticesPerSide + 1, verticesPerSide, 0];

        for (var f = 0; f < 6; f++)
        {
            for (var v = 0; v < verticesPerSide; v++)
            {
                var vOffset = (float)v / (verticesPerSide - 1);

                for (var u = 0; u < verticesPerSide; u++)
                {
                    var uOffset = (float)u / (verticesPerSide - 1);

                    var cubePos = Origins[f] + 2 * uOffset * UDirections[f] + 2 * vOffset * VDirections[f];

                    var xSq = cubePos.X * cubePos.X;
                    var ySq = cubePos.Y * cubePos.Y;
                    var zSq = cubePos.Z * cubePos.Z;

                    var spherePos = new float3(
                        cubePos.X * float.Sqrt(1 - (ySq / 2) - (zSq / 2) + (ySq * zSq / 3)),
                        cubePos.Y * float.Sqrt(1 - (zSq / 2) - (xSq / 2) + (zSq * xSq / 3)),
                        cubePos.Z * float.Sqrt(1 - (xSq / 2) - (ySq / 2) + (xSq * ySq / 3))
                    );
                    
                    vertices[u + v * verticesPerSide + f * verticesPerFace] = new Vertex
                    {
                        Position = spherePos * radius,
                        Normal = spherePos,
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
                            (uint)(baseIndices[i] + u + v * verticesPerSide + f * verticesPerFace);
                    }
                }
            }
        }

        return new Mesh(vertices, indices);
    }
}