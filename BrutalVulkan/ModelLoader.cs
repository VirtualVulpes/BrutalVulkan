using System.Runtime.InteropServices;
using Brutal.GltfApi;
using Brutal.Numerics;
using BrutalVulkan.Rendering.Meshes;
using Mesh = BrutalVulkan.Rendering.Meshes.Mesh;

namespace BrutalVulkan;

public static class ModelLoader
{
    public static Mesh LoadModel(String fileName)
    {
        var filePath = Path.Join("models", fileName);

        var loader = new GltfLoader(filePath);
        
        var positionAccessor = loader.GltfJson.Meshes[0].Primitives[0].Attributes["POSITION"];
        var positionData = loader.GetAccessorBufferSpan(positionAccessor);
        var positions = MemoryMarshal.Cast<Byte, float3>(positionData).ToArray();
        
        var normalAccessor = loader.GltfJson.Meshes[0].Primitives[0].Attributes["NORMAL"];
        var normalData = loader.GetAccessorBufferSpan(normalAccessor);
        var normals = MemoryMarshal.Cast<Byte, float3>(normalData).ToArray();

        var uvAccessor = loader.GltfJson.Meshes[0].Primitives[0].Attributes["TEXCOORD_0"];
        var uvData = loader.GetAccessorBufferSpan(uvAccessor);
        var uvs = MemoryMarshal.Cast<Byte, float2>(uvData).ToArray();
        
        var vertices = new Vertex[positions.Length];
        
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vertex
            {
                Position = positions[i],
                Normal = normals[i],
                Uv = uvs[i]
            };
        }
        
        var indexAccessorIndex = loader.GltfJson.Meshes[0].Primitives[0].Indices.Value;
        var indexAccessor = loader.GltfJson.Accessors[indexAccessorIndex];
        var indexData = loader.GetAccessorBufferSpan(indexAccessorIndex);

        var indices = new UInt32[indexAccessor.Count];

        switch ((Int32)indexAccessor.ComponentType)
        {
            case 5121:
            {
                var data = MemoryMarshal.Cast<Byte, Byte>(indexData);

                for (var i = 0; i < indices.Length; i++)
                {
                    indices[i] = data[i];
                }

                break;
            }

            case 5123:
            {
                var data = MemoryMarshal.Cast<Byte, UInt16>(indexData);
                
                for (var i = 0; i < indices.Length; i++)
                {
                    indices[i] = data[i];
                }
                
                break;
            }

            case 5125:
            {
                var data = MemoryMarshal.Cast<Byte, UInt32>(indexData);
                
                data[..indices.Length].CopyTo(indices);
                
                break;
            }

            default:
                throw new NotSupportedException("Unsupported index component type");
        }

        return new Mesh(vertices, indices);
    }
}