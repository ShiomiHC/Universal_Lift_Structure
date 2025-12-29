namespace Universal_Lift_Structure;

// 利用 using 语句自动管理 SimplePool 列表的租借和归还
// 注意：由于是 ref struct，不能存储为字段或捕获到闭包中
public readonly ref struct PooledList<T>
{
    public readonly List<T> List;

    public PooledList(out List<T> list)
    {
        List = SimplePool<List<T>>.Get();
        List.Clear();
        list = List;
    }

    public void Dispose()
    {
        List.Clear();
        SimplePool<List<T>>.Return(List);
    }
}

// HashSet 版本的对象池包装器
public readonly ref struct PooledHashSet<T>
{
    public readonly HashSet<T> Set;

    public PooledHashSet(out HashSet<T> set)
    {
        Set = SimplePool<HashSet<T>>.Get();
        Set.Clear();
        set = Set;
    }

    public void Dispose()
    {
        Set.Clear();
        SimplePool<HashSet<T>>.Return(Set);
    }
}
