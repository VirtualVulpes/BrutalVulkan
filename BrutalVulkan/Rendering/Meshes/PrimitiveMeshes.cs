using Armature.Rendering;
using Brutal.Numerics;

namespace BrutalVulkan.Rendering.Meshes;

public static class PrimitiveMeshes
{
    public static Mesh CreatePlane()
    {
        return new Mesh(
            [
                new Vertex(new float3(-0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(0.5f, -0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(-0.5f, 0.5f, 0.0f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 0.0f))
            ],
            [
                0, 1, 2, 2, 3, 0
            ]
        );
    }

    public static Mesh CreateCube()
    {
        return new Mesh(
            [
                new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(0.0f, 0.0f, 1.0f), new float2(0.0f, 0.0f)),

                new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(0.0f, 0.0f, -1.0f), new float2(0.0f, 0.0f)),

                new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(1.0f, 0.0f, 0.0f), new float2(0.0f, 0.0f)),

                new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(-1.0f, 0.0f, 0.0f), new float2(0.0f, 0.0f)),

                new Vertex(new float3(-0.5f, 0.5f, 0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(0.5f, 0.5f, 0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(0.5f, 0.5f, -0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(-0.5f, 0.5f, -0.5f), new float3(0.0f, 1.0f, 0.0f), new float2(0.0f, 0.0f)),

                new Vertex(new float3(-0.5f, -0.5f, -0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(0.0f, 1.0f)),
                new Vertex(new float3(0.5f, -0.5f, -0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(1.0f, 1.0f)),
                new Vertex(new float3(0.5f, -0.5f, 0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(1.0f, 0.0f)),
                new Vertex(new float3(-0.5f, -0.5f, 0.5f), new float3(0.0f, -1.0f, 0.0f), new float2(0.0f, 0.0f))
            ],
            [
                0, 1, 2, 2, 3, 0,
                4, 5, 6, 6, 7, 4,
                8, 9, 10, 10, 11, 8,
                12, 13, 14, 14, 15, 12,
                16, 17, 18, 18, 19, 16,
                20, 21, 22, 22, 23, 20
            ]
        );
    }
}