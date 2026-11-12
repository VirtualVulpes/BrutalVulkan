using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan;

public readonly record struct MeshId(UInt32 Value)
{
    public static readonly MeshId Invalid = new(UInt32.MaxValue);
}

public sealed class MeshManager : IDisposable
{
    private UInt32 _nextId = 0;
    private readonly VmaAllocator _allocator;
    
    private readonly Dictionary<MeshId, Mesh> _meshes = new();
    private readonly Dictionary<MeshId, GpuMesh> _gpuMeshes = new();
    private readonly Dictionary<String, MeshId> _names = new();

    public MeshManager(VmaAllocator allocator)
    {
        _allocator = allocator;
    }
    
    public MeshId AddMesh(String name, Mesh mesh)
    {
        var meshId = new MeshId(_nextId++);
        
        _meshes.Add(meshId, mesh);
        _names.Add(name, meshId);

        UploadMesh(meshId);
        
        return meshId;
    }

    private void UploadMesh(MeshId meshId)
    {
        _gpuMeshes.Add(meshId, new GpuMesh(_allocator, _meshes[meshId]));
    }
    
    public Mesh GetMesh(MeshId meshId)
    {
        return _meshes[meshId];
    }

    public GpuMesh GetGpuMesh(MeshId meshId)
    {
        return _gpuMeshes[meshId];
    }

    public MeshId GetMeshId(String name)
    {
        return _names[name];
    }

    public void Dispose()
    {
        foreach (var mesh in _gpuMeshes.Values)
        {
            mesh.Dispose();
        }
    }
}