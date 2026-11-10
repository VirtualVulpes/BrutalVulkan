using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;

namespace BrutalVulkan;

public sealed class GpuMesh : IDisposable
{
    private readonly VmaAllocator _allocator;
    
    public UInt16 IndexCount { get; }

    private VkBuffer _vertexBuffer;
    public VkBuffer VertexBuffer => _vertexBuffer;
    private readonly VmaAllocation _vertexAllocation;

    private VkBuffer _indexBuffer;
    public VkBuffer IndexBuffer => _indexBuffer;
    private readonly VmaAllocation _indexAllocation;
    
    public unsafe GpuMesh(VmaAllocator allocator, Mesh mesh)
    {
        _allocator = allocator;
        
        var verticesSize = ByteSize.Of(mesh.Vertices);
        _allocator.CreateBuffer(
            new VkBufferCreateInfo { Size = verticesSize, Usage = VkBufferUsageFlags.VertexBufferBit },
            new VmaAllocationCreateInfo
            {
                Usage = VmaMemoryUsage.Auto,
                Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit
            },
            out _vertexBuffer,
            out _vertexAllocation,
            out var vertexAllocationInfo
        );
        mesh.Vertices.CopyTo(new Span<Vertex>((void*)vertexAllocationInfo.PMappedData, mesh.Vertices.Length));
        _allocator.FlushAllocation(_vertexAllocation, ByteSize.Zero, verticesSize);

        var indicesSize = ByteSize.Of(mesh.Indices);
        _allocator.CreateBuffer(
            new VkBufferCreateInfo { Size = indicesSize, Usage = VkBufferUsageFlags.IndexBufferBit },
            new VmaAllocationCreateInfo
            {
                Usage = VmaMemoryUsage.Auto,
                Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit
            },
            out _indexBuffer,
            out _indexAllocation,
            out var indexAllocationInfo
        );
        mesh.Indices.CopyTo(new Span<UInt16>((void*)indexAllocationInfo.PMappedData, mesh.Indices.Length));
        _allocator.FlushAllocation(_indexAllocation, ByteSize.Zero, indicesSize);

        IndexCount = (UInt16)mesh.Indices.Length;
    }

    public void Dispose()
    {
        _allocator.DestroyBuffer(_vertexBuffer, _vertexAllocation);
        _allocator.DestroyBuffer(_indexBuffer, _indexAllocation);
    }
}