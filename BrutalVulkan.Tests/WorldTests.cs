using BrutalVulkan.ECS;

namespace BrutalVulkan.Tests;

public class WorldTests
{
    private struct TestComponent
    {
        public Int32 Value;
    }
    
    private struct TestComponent2
    {
        public Int32 Value;
    }

    [Fact]
    public void CreateEntity_CreatesAliveEntity()
    {
        var world = new World();

        var entity = world.CreateEntity();

        Assert.True(world.IsEntityAlive(entity));
    }

    [Fact]
    public void CreateEntity_CreatesUniqueEntities()
    {
        var world = new World();

        var first = world.CreateEntity();
        var second = world.CreateEntity();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DestroyEntity_InvalidatesEntity()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.DestroyEntity(entity);

        Assert.False(world.IsEntityAlive(entity));
    }

    [Fact]
    public void HasComponent_InvalidEntity_Throws()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.AddComponent(entity, new TestComponent { Value = 1 });

        world.DestroyEntity(entity);

        Assert.Throws<InvalidOperationException>(() => 
            world.HasComponent<TestComponent>(entity));
    }

    [Fact]
    public void CreateEntity_ReusesDestroyedId()
    {
        var world = new World();

        var first = world.CreateEntity();
        world.DestroyEntity(first);

        var second = world.CreateEntity();

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Generation, second.Generation);
    }

    [Fact]
    public void DestroyEntity_OldHandleCannotAccessReusedEntity()
    {
        var world = new World();

        var first = world.CreateEntity();
        world.AddComponent(first, new TestComponent { Value = 1 });

        world.DestroyEntity(first);

        var second = world.CreateEntity();
        world.AddComponent(second, new TestComponent { Value = 2 });

        Assert.False(world.IsEntityAlive(first));
        Assert.True(world.IsEntityAlive(second));
        Assert.Throws<InvalidOperationException>(() => 
            world.HasComponent<TestComponent>(first));
        Assert.Equal(2, world.GetComponent<TestComponent>(second).Value);
    }

    [Fact]
    public void AddComponent_InvalidEntity_Throws()
    {
        var world = new World();

        var entity = world.CreateEntity();
        world.DestroyEntity(entity);

        Assert.Throws<InvalidOperationException>(() =>
            world.AddComponent(entity, new TestComponent { Value = 1 }));
    }
    
    [Fact]
    public void Query_TwoComponent_ValidSize()
    {
        var world = new World();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new TestComponent{Value = 1});
        world.AddComponent(e1, new TestComponent2{Value = 2});

        var e2 = world.CreateEntity();
        world.AddComponent(e2, new TestComponent2{Value = 3});
        
        var query = world.Query<TestComponent, TestComponent2>();
        Assert.Equal(1, query.MaxCount); 
    }
    
    [Fact]
    public void Query_TwoComponent_ValidResult()
    {
        var world = new World();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new TestComponent{Value = 1});
        world.AddComponent(e1, new TestComponent2{Value = 2});

        var e2 = world.CreateEntity();
        world.AddComponent(e2, new TestComponent2{Value = 3});
        
        var e3 = world.CreateEntity();
        world.AddComponent(e3, new TestComponent{Value = 6});
        world.AddComponent(e3, new TestComponent2{Value = 3});

        
        var query = world.Query<TestComponent, TestComponent2>();
        foreach (var item in query)
        {
            Assert.True(world.HasComponent<TestComponent>(item.Entity));
            Assert.True(world.HasComponent<TestComponent2>(item.Entity));
        }
    }
}