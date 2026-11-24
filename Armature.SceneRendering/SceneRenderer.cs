using Armature.Core;
using Armature.Rendering;
using Armature.Vulkan;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using CommunityToolkit.HighPerformance;

namespace Armature.SceneRendering;

public sealed class SceneRenderer : IFrameListener, IDisposable
{
    private readonly PipelineManager _pipelines;
    private readonly MeshManager _meshes;
    private readonly VulkanContext _context;
    private readonly SceneFrameResources[] _resources = new SceneFrameResources[RenderConstants.MaxFramesInFlight];
    private readonly List<RenderItem> _items = [];

    private GpuCamera _camera;
    private GpuLight[] _lights = [];
    private int _lightCount;

    public ReadOnlySpan<RenderItem> Items => _items.AsSpan();
    public float4x4 ViewProj => _camera.ViewProj;

    public SceneRenderer(PipelineManager pipelines, MeshManager meshes, VulkanContext context,
        CoreDescriptors descriptors)
    {
        _pipelines = pipelines;
        _meshes = meshes;
        _context = context;

        for (var i = 0; i < _resources.Length; i++)
        {
            _resources[i] = new SceneFrameResources
            {
                PerFrameDescriptorSet = descriptors.AllocatePerFrameDescriptorSet()
            };
        }

        CreatePerFrameBuffers();
        UpdateFrameDescriptors();
    }

    private void CreatePerFrameBuffers()
    {
        foreach (var r in _resources)
        {
            var lightBuffer = new BufferEx.CreateInfo
            {
                Name = "Lights Storage Buffer",
                BufferSize = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
                BufferUsage = VkBufferUsageFlags.StorageBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.LightsBuffer = _context.Device.CreateBuffer(lightBuffer);
            r.LightsMemory = r.LightsBuffer.Map();

            var cameraBuffer = new BufferEx.CreateInfo
            {
                Name = "Camera Buffer",
                BufferSize = ByteSize.Of<GpuCamera>(),
                BufferUsage = VkBufferUsageFlags.UniformBufferBit,
                AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
            };
            r.CameraBuffer = _context.Device.CreateBuffer(cameraBuffer);
            r.CameraMemory = r.CameraBuffer.Map();
        }
    }

    private unsafe void UpdateFrameDescriptors()
    {
        foreach (var r in _resources)
        {
            var lightsInfo = new VkDescriptorBufferInfo
            {
                Buffer = r.LightsBuffer.VkBuffer,
                Offset = ByteSize.Zero,
                Range = ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights
            };

            var cameraInfo = new VkDescriptorBufferInfo
            {
                Buffer = r.CameraBuffer.VkBuffer,
                Offset = ByteSize.Zero,
                Range = ByteSize.Of<GpuCamera>()
            };

            ReadOnlySpan<VkWriteDescriptorSet> descriptorWrites =
            [
                new()
                {
                    DstSet = r.PerFrameDescriptorSet,
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.StorageBuffer,
                    BufferInfo = &lightsInfo
                },
                new()
                {
                    DstSet = r.PerFrameDescriptorSet,
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.UniformBuffer,
                    BufferInfo = &cameraInfo
                }
            ];

            _context.Device.UpdateDescriptorSets(descriptorWrites, null);
        }
    }

    private unsafe void UpdateLights(SceneFrameResources r, ReadOnlySpan<GpuLight> lights)
    {
        if (lights.Length > RenderConstants.MaxLights)
            throw new ArgumentOutOfRangeException(nameof(lights));

        var count = lights.Length;

        Buffer.MemoryCopy(
            &count,
            r.LightsMemory.MapPtr.Pointer,
            ByteSize.Of<UInt32>() * 4 + ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
            ByteSize.Of<UInt32>()
        );

        fixed (GpuLight* lightsPtr = lights)
        {
            Buffer.MemoryCopy(
                lightsPtr,
                (Byte*)r.LightsMemory.MapPtr.Pointer + ByteSize.Of<UInt32>() * 4,
                ByteSize.Of<GpuLight>() * RenderConstants.MaxLights,
                ByteSize.Of<GpuLight>() * lights.Length
            );
        }
    }

    private unsafe void UpdateCamera(SceneFrameResources r, GpuCamera camera)
    {
        Buffer.MemoryCopy(
            &camera,
            r.CameraMemory.MapPtr.Pointer,
            ByteSize.Of<GpuCamera>(),
            ByteSize.Of<GpuCamera>()
        );
    }

    public void Submit(SceneData scene)
    {
        _camera = scene.Camera;
        _lights = scene.Lights;
        _lightCount = scene.LightCount;

        _items.Clear();
        foreach (var o in scene.ObjectSpan)
        {
            _items.Add(new RenderItem
                {
                    Pipeline = _pipelines.GetPipeline(o.PipelineId),
                    GpuMesh = _meshes.GetGpuMesh(o.MeshId),
                    ModelMatrix = o.ModelMatrix,
                    TextureId = o.TextureId,
                    Color = o.Color,
                    DataType = o.DataType,
                    Flags = o.Flags,
                    ItemId = o.ObjectId
                }
            );
        }
    }

    public void OnPrepare(ref RenderContext ctx)
    {
        var r = _resources[ctx.FrameIndex];
        UpdateLights(r, _lights.AsSpan(0, _lightCount));
        UpdateCamera(r, _camera);
        ctx.PerFrameDescriptorSet = r.PerFrameDescriptorSet;
    }
    
    public void Dispose()
    {
        foreach (var r in _resources)
        {
            r.LightsMemory.Unmap();
            r.LightsBuffer.Dispose();
            r.CameraMemory.Unmap();
            r.CameraBuffer.Dispose();
        }
    }
}