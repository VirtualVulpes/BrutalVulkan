namespace BrutalVulkan.ECS.Systems;

public class SystemManager
{
    private readonly List<ISystem> _systems = [];

    public T AddSystem<T>(T system) where T : ISystem
    {
        _systems.Add(system);
        return system;
    }

    public T GetSystem<T>() where T : ISystem
    {
        return _systems.OfType<T>().First();
    }

    public void Start()
    {
        foreach (var system in _systems)
        {
            system.Start();
        }
    }

    public void Update(Single deltaTime)
    {
        foreach (var system in _systems)
        {
            system.Update(deltaTime);
        }
    }
}