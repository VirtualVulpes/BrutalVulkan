using Brutal.Numerics;

namespace BrutalVulkan;

public static class CubeSphere
{
    public static Mesh CreateMesh(int subdivisions)
    {
        var startX = -0.5f;
        var startY = -0.5f;

        float3[] startPositions =
        [
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
            new float3(-0.5f, -0.5f, 0.5f),
        ];

        var verticesPerSide = (1 << subdivisions) + 1;
        var vertices = new Vertex[verticesPerSide * verticesPerSide * 6];

        var squaresPerSide = verticesPerSide - 1;

        var numIndices = squaresPerSide * squaresPerSide * 6;
        var indices = new UInt16[numIndices];
        
        int[] baseIndices = [0, 1, 3, 3, 2, 0];
        int[] yMask = [0, 0, 1, 1, 1, 0];

        for (int i = 0; i < 6; i++)
        {
            baseIndices[i] += yMask[i] * (verticesPerSide - 2);
        }
        
        for (int y = 0; y < verticesPerSide; y++)
        {
            var yPos = startY + ((float)y / (verticesPerSide - 1));
            for (int x = 0; x < verticesPerSide; x++)
            {
                var xPos = startX + ((float)x / (verticesPerSide - 1));

                var vertex = new Vertex
                {
                    Position = new float3(xPos, yPos, 0.0f),
                    Normal = float3.Zero,
                    Uv = float2.Zero
                };

                vertices[x + y * verticesPerSide] = vertex;
            }
        }
        
        for (int y = 0; y < squaresPerSide; y++)
        {
            for (int x = 0; x < squaresPerSide; x++)
            {
                for (int i = 0; i < 6; i++)
                {
                    indices[i + x * 6 + y * 6 * squaresPerSide] =
                        (ushort)(baseIndices[i] + x + y * verticesPerSide);
                }
            }
        }

        return new Mesh(vertices, indices);
    }
}