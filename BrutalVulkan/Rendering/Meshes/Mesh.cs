namespace BrutalVulkan.Rendering.Meshes;

public sealed class Mesh
{
    private readonly Vertex[] _vertices;
    public ReadOnlySpan<Vertex> Vertices => _vertices;

    private readonly UInt32[] _indices;
    public ReadOnlySpan<UInt32> Indices => _indices;

    public Mesh(Vertex[] vertices, UInt32[] indices)
    {
        _vertices = (Vertex[])vertices.Clone();
        _indices = (UInt32[])indices.Clone();
    }
}