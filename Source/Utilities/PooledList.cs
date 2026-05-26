namespace Universal_Lift_Structure;

// ref struct 包装器，配合 using 语句自动归还 List<T> 到对象池。
// 约束：只能作为局部变量或方法参数，不能用于异步方法或迭代器。
// 归还后不应再访问 List，否则可能导致数据污染。
//
// 典型用法：
// using (new PooledList<int>(out var list)) { list.Add(1); }
public readonly ref struct PooledList<T>
{
    public readonly List<T> List;

    public PooledList(out List<T> list)
    {
        List = SimplePool<List<T>>.Get();
        List.Clear(); // 池中实例可能有残留数据
        list = List;
    }

    public void Dispose()
    {
        List.Clear();
        SimplePool<List<T>>.Return(List);
    }
}

// HashSet 版本，适用于去重和 O(1) 成员检查场景。
// 同样的 ref struct 约束和归还规则适用。
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
