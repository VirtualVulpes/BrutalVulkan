using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Vma;

namespace Armature.Vulkan;

public sealed class GpuMesh : IDisposable
{
    private readonly VmaAllocator _allocator;
    private readonly VmaAllocation _vertexAllocation;
    private readonly VmaAllocation _indexAllocation;

    public VkBuffer VertexBuffer { get; }
    public VkBuffer IndexBuffer { get; }
    public int IndexCount { get; }

    public GpuMesh(VmaAllocator allocator, VkBuffer vb, VmaAllocation va, VkBuffer ib, VmaAllocation ia, int indexCount)
    {
        _allocator = allocator;
        VertexBuffer = vb;
        _vertexAllocation = va;
        IndexBuffer = ib;
        _indexAllocation = ia;
        IndexCount = indexCount;
    }

    public static GpuMesh Create<TVertex>(VmaAllocator allocator, ReadOnlySpan<TVertex> vertices,
        ReadOnlySpan<uint> indices) where TVertex : unmanaged
    {
        CreateUploaded(allocator, vertices, VkBufferUsageFlags.VertexBufferBit, out var vb, out var va);
        CreateUploaded(allocator, indices, VkBufferUsageFlags.IndexBufferBit, out var ib, out var ia);
        return new GpuMesh(allocator, vb, va, ib, ia, indices.Length);
    }

    private static unsafe void CreateUploaded<T>(VmaAllocator allocator, ReadOnlySpan<T> data, VkBufferUsageFlags usage,
        out VkBuffer buffer, out VmaAllocation allocation) where T : unmanaged
    {
        var size = ByteSize.Of<T>() * data.Length;
        allocator.CreateBuffer(
            new VkBufferCreateInfo { Size = size, Usage = usage },
            new VmaAllocationCreateInfo
            {
                Usage = VmaMemoryUsage.Auto,
                Flags = VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit
            },
            out buffer, out allocation, out var info);
        
        data.CopyTo(new Span<T>(info.PMappedData.Pointer, data.Length));
        allocator.FlushAllocation(allocation, ByteSize.Zero, size);
    }

    public void Dispose()
    {
        _allocator.DestroyBuffer(VertexBuffer, _vertexAllocation);
        _allocator.DestroyBuffer(IndexBuffer, _indexAllocation);
    }
}