namespace Armature.Core;

public readonly record struct PipelineId(uint Value)
{
    public static readonly PipelineId Invalid = new(uint.MaxValue);
}
