namespace Armature.Core;

public readonly record struct TextureId(uint Value)
{
    public static readonly TextureId Invalid = new(uint.MaxValue);
}
