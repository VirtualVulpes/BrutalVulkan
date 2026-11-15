namespace BrutalVulkan;

public sealed class Mesh
{
    private Vertex[] _vertices;
    public ReadOnlySpan<Vertex> Vertices => _vertices;

    private UInt32[] _indices;
    public ReadOnlySpan<UInt32> Indices => _indices;

    public Mesh(Vertex[] vertices, UInt32[] indices)
    {
        _vertices = (Vertex[])vertices.Clone();
        _indices = (UInt32[])indices.Clone();
    }
}