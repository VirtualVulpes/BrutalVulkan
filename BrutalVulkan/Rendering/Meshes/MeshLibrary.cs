using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.Rendering.Meshes;

public static class MeshLibrary
{
    public static void RegisterDefaults(MeshManager manager)
    {
        manager.AddMesh("plane", PrimitiveMeshes.CreatePlane());
        manager.AddMesh("cube", PrimitiveMeshes.CreateCube());

        CubeSphere.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        manager.AddMesh("planet", CubeSphere.CreateNoiseMesh(3, 2.0f));
        
        manager.AddMesh("sphere", CubeSphere.CreateMesh(3));
        manager.AddMesh("face", CubeSphere.CreateChunkMesh(Face.Front, 0, 0, 0));
        manager.AddMesh("sub_face_0_0", CubeSphere.CreateChunkMesh(Face.Front, 1, 0, 0));
        manager.AddMesh("sub_face_1_0", CubeSphere.CreateChunkMesh(Face.Front, 2, 2, 0));
    }
}