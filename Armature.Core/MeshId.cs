namespace Armature.Core;

public readonly record struct MeshId(uint Value)
{
    public static readonly MeshId Invalid = new(uint.MaxValue);
}
