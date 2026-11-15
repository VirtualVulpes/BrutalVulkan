namespace BrutalVulkan.ECS;

public interface IComponentPool
{
    Boolean Remove(Entity entity);
    Boolean Has(Entity entity);
    Int32 Count { get; }
    Entity GetEntityAt(Int32 index);
}