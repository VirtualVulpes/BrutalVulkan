namespace BrutalVulkan.ECS;

public readonly record struct EntityId(UInt32 Value)
{
    public static readonly EntityId Invalid = new(UInt32.MaxValue);
}

public readonly record struct Entity(EntityId Id, UInt32 Generation)
{
    public override String ToString()
    {
        return $"Entity({Id.Value},{Generation})";
    }
}