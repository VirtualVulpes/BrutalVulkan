namespace Armature.Rendering;

public static class MeshLibrary
{
    public static void RegisterDefaults(MeshManager manager)
    {
        manager.AddMesh("plane", PrimitiveMeshes.CreatePlane());
        manager.AddMesh("cube", PrimitiveMeshes.CreateCube());
        manager.AddMesh("sphere", CubeSphere.CreateMesh(3, 2));
        manager.AddMesh("sphere_low_res", CubeSphere.CreateMesh(1));
    }
}