namespace Armature.Scene;

public readonly record struct EntityId(UInt32 Value)
{
    public static readonly EntityId Invalid = new(UInt32.MaxValue);
}

public readonly record struct Entity(EntityId Id, UInt32 Generation)
{
    public static readonly Entity Invalid = new Entity
    {
        Id = EntityId.Invalid,
        Generation = 0
    };

    public override String ToString()
    {
        return $"Entity({Id.Value},{Generation})";
    }
}