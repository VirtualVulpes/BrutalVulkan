namespace BrutalVulkan.ECS.Components;

public enum Face
{
    Front,
    Right,
    Back,
    Left,
    Top,
    Bottom
}

public record struct NodeComponent
{
    public Face Face;
    public UInt32 Level;
    public UInt32 X;
    public UInt32 Y;
}