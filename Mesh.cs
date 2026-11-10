namespace BrutalVulkan;

public sealed class Mesh
{
    private Vertex[] _vertices;
    public ReadOnlySpan<Vertex> Vertices => _vertices;

    private UInt16[] _indices;
    public ReadOnlySpan<UInt16> Indices => _indices;

    public Mesh(Vertex[] vertices, UInt16[] indices)
    {
        _vertices = (Vertex[])vertices.Clone();
        _indices = (UInt16[])indices.Clone();
    }
}