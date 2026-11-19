namespace BrutalVulkan.ECS.Systems;

public interface ISystem
{
    void Start();
    void Update(Single deltaTime);
}