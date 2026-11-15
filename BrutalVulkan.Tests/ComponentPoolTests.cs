using BrutalVulkan.ECS;

namespace BrutalVulkan.Tests;

public class ComponentPoolTests
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
    public void Add_AddsComponent()
    {
        var pool = new ComponentPool<TestComponent>();
        var entity = new Entity(new EntityId(0), 0);
        
        pool.Add(entity, new TestComponent{Value = 1});
        
        Assert.True(pool.Has(entity));
        Assert.Equal(1, pool.Get(entity).Value);
    }

    [Fact] public void Add_MultipleComponents()
    {
        var pool = new ComponentPool<TestComponent>();
        var pool2 = new ComponentPool<TestComponent2>();
        var entity = new Entity(new EntityId(0), 0);
        
        pool.Add(entity, new TestComponent{Value = 1});
        pool2.Add(entity, new TestComponent2{Value = 1});
        
        Assert.True(pool.Has(entity));
        Assert.Equal(1, pool.Get(entity).Value);
        
        Assert.True(pool2.Has(entity));
        Assert.Equal(1, pool2.Get(entity).Value);
    }
    
    [Fact]
    public void Add_DuplicateComponent_Throws()
    {
        var pool = new ComponentPool<TestComponent>();
        var entity = new Entity(new EntityId(0), 0);
        
        pool.Add(entity, new TestComponent{Value = 1});
        
        Assert.Throws<InvalidOperationException>(() => 
            pool.Add(entity, new TestComponent{Value = 2}));
    }
    
    [Fact]
    public void Add_CapacityResize()
    {
        var pool = new ComponentPool<TestComponent>();

        for (UInt32 i = 0; i < 10000; i++)
        {
            var entity = new Entity(new EntityId(i), 0);
            pool.Add(entity, new TestComponent{Value = 1});
            
            Assert.True(pool.Has(entity));
            Assert.Equal(1, pool.Get(entity).Value);
        }
    }
    
    [Fact]
    public void Remove_RemovesComponent()
    {
        var pool = new ComponentPool<TestComponent>();
        var entity = new Entity(new EntityId(0), 0);

        pool.Add(entity, new TestComponent { Value = 1 });

        var result = pool.Remove(entity);

        Assert.True(result);
        Assert.False(pool.Has(entity));
    }

    [Fact]
    public void Remove_MissingComponent_ReturnsFalse()
    {
        var pool = new ComponentPool<TestComponent>();
        var entity = new Entity(new EntityId(0), 0);

        Assert.False(pool.Remove(entity));
    }

    [Fact]
    public void Remove_MiddleElement_PreservesOtherComponents()
    {
        var pool = new ComponentPool<TestComponent>();

        var first = new Entity(new EntityId(0), 0);
        var middle = new Entity(new EntityId(1), 0);
        var last = new Entity(new EntityId(2), 0);

        pool.Add(first, new TestComponent { Value = 1 });
        pool.Add(middle, new TestComponent { Value = 2 });
        pool.Add(last, new TestComponent { Value = 3 });

        pool.Remove(middle);

        Assert.Equal(1, pool.Get(first).Value);
        Assert.Equal(3, pool.Get(last).Value);
        Assert.False(pool.Has(middle));
    }

    [Fact]
    public void Has_DifferentGeneration_ReturnsFalse()
    {
        var pool = new ComponentPool<TestComponent>();

        var entity = new Entity(new EntityId(0), 0);
        var staleEntity = new Entity(new EntityId(0), 1);

        pool.Add(entity, new TestComponent { Value = 1 });

        Assert.False(pool.Has(staleEntity));
    }
}
