namespace BrutalVulkan.ECS;

// 1 component

public readonly ref struct QueryItem<T1> where T1 : struct
{
    public readonly Entity Entity;
    public readonly ref T1 C1;

    public QueryItem(Entity entity, ref T1 c1)
    {
        Entity = entity;
        C1 = ref c1;
    }
}

public readonly ref struct Query<T1> where T1 : struct
{
    private readonly ComponentPool<T1> _pool;

    public Query(ComponentPool<T1> pool) => _pool = pool;

    public Enumerator GetEnumerator() => new(_pool);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _pool;
        private Int32 _index;

        public Enumerator(ComponentPool<T1> pool)
        {
            _pool = pool;
            _index = pool.Count;
        }

        public Boolean MoveNext() => --_index >= 0;

        public QueryItem<T1> Current =>
            new(_pool.GetEntityAt(_index), ref _pool.GetAt(_index));
    }
    
    public Int32 MaxCount => _pool.Count;
}

// 2 components

public readonly ref struct QueryItem<T1, T2>
    where T1 : struct where T2 : struct
{
    public readonly Entity Entity;
    public readonly ref T1 C1;
    public readonly ref T2 C2;

    public QueryItem(Entity entity, ref T1 c1, ref T2 c2)
    {
        Entity = entity;
        C1 = ref c1;
        C2 = ref c2;
    }
}

public readonly ref struct Query<T1, T2>
    where T1 : struct where T2 : struct
{
    private readonly ComponentPool<T1> _p1;
    private readonly ComponentPool<T2> _p2;

    public Query(ComponentPool<T1> p1, ComponentPool<T2> p2)
    {
        _p1 = p1;
        _p2 = p2;
    }

    public Enumerator GetEnumerator() => new(_p1, _p2);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _p1;
        private readonly ComponentPool<T2> _p2;
        private readonly Boolean _driveFirst;
        private Int32 _index;
        private Int32 _i1;
        private Int32 _i2;
        private Entity _entity;

        public Enumerator(ComponentPool<T1> p1, ComponentPool<T2> p2)
        {
            _p1 = p1;
            _p2 = p2;
            _driveFirst = p1.Count <= p2.Count;
            _index = _driveFirst ? p1.Count : p2.Count;
            _i1 = _i2 = -1;
            _entity = Entity.Invalid;
        }

        public Boolean MoveNext()
        {
            while (--_index >= 0)
            {
                if (_driveFirst)
                {
                    var e = _p1.GetEntityAt(_index);
                    if (!_p2.TryGetIndex(e, out _i2)) continue;
                    _i1 = _index;
                    _entity = e;
                }
                else
                {
                    var e = _p2.GetEntityAt(_index);
                    if (!_p1.TryGetIndex(e, out _i1)) continue;
                    _i2 = _index;
                    _entity = e;
                }

                return true;
            }

            return false;
        }

        public QueryItem<T1, T2> Current =>
            new(_entity, ref _p1.GetAt(_i1), ref _p2.GetAt(_i2));
    }
    
    public Int32 MaxCount => Math.Min(_p1.Count, _p2.Count);
}

// 3 components

public readonly ref struct QueryItem<T1, T2, T3>
    where T1 : struct where T2 : struct where T3 : struct
{
    public readonly Entity Entity;
    public readonly ref T1 C1;
    public readonly ref T2 C2;
    public readonly ref T3 C3;

    public QueryItem(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3)
    {
        Entity = entity;
        C1 = ref c1;
        C2 = ref c2;
        C3 = ref c3;
    }
}

public readonly ref struct Query<T1, T2, T3>
    where T1 : struct where T2 : struct where T3 : struct
{
    private readonly ComponentPool<T1> _p1;
    private readonly ComponentPool<T2> _p2;
    private readonly ComponentPool<T3> _p3;

    public Query(ComponentPool<T1> p1, ComponentPool<T2> p2, ComponentPool<T3> p3)
    {
        _p1 = p1;
        _p2 = p2;
        _p3 = p3;
    }

    public Enumerator GetEnumerator() => new(_p1, _p2, _p3);

    public ref struct Enumerator
    {
        private readonly ComponentPool<T1> _p1;
        private readonly ComponentPool<T2> _p2;
        private readonly ComponentPool<T3> _p3;
        private readonly Int32 _driver;
        private Int32 _index;
        private Int32 _i1;
        private Int32 _i2;
        private Int32 _i3;
        private Entity _entity;

        public Enumerator(ComponentPool<T1> p1, ComponentPool<T2> p2, ComponentPool<T3> p3)
        {
            _p1 = p1;
            _p2 = p2;
            _p3 = p3;
            _driver = 1;
            var min = p1.Count;
            if (p2.Count < min)
            {
                _driver = 2;
                min = p2.Count;
            }

            if (p3.Count < min)
            {
                _driver = 3;
                min = p3.Count;
            }

            _index = min;
            _i1 = _i2 = _i3 = -1;
            _entity = Entity.Invalid;
        }

        public Boolean MoveNext()
        {
            while (--_index >= 0)
            {
                Entity e;

                switch (_driver)
                {
                    case 1:
                        e = _p1.GetEntityAt(_index);
                        _i1 = _index;
                        if (!_p2.TryGetIndex(e, out _i2) ||
                            !_p3.TryGetIndex(e, out _i3)) continue;
                        break;
                    case 2:
                        e = _p2.GetEntityAt(_index);
                        _i2 = _index;
                        if (!_p1.TryGetIndex(e, out _i1) ||
                            !_p3.TryGetIndex(e, out _i3)) continue;                        
                        break;
                    default:
                        e = _p3.GetEntityAt(_index);
                        _i3 = _index;
                        if (!_p1.TryGetIndex(e, out _i1) ||
                            !_p2.TryGetIndex(e, out _i2)) continue;                        
                        break;
                }

                _entity = e;
                return true;
            }

            return false;
        }

        public QueryItem<T1, T2, T3> Current =>
            new(_entity, ref _p1.GetAt(_i1), ref _p2.GetAt(_i2), ref _p3.GetAt(_i3));
    }
    
    public Int32 MaxCount => Math.Min(_p1.Count, Math.Min(_p2.Count, _p3.Count));
}