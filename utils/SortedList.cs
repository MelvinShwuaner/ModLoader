using System.Collections;

namespace NeoModLoader.utils;
/// <summary>
/// A list which automatically sorts itself when modified
/// </summary>
public class SortedList<T> : IList<T>
{
    private Comparison<T> Comparater;
    private List<T> List = new();
    public SortedList(Comparison<T> comparater)
    {
        Comparater =  comparater;
    }
    private SortedList()
    {
    }
    public SortedList(Comparison<T> comparater, List<T> list)
    {
        Comparater =  comparater;
        List = list;
        Sort();
    }
    /// <summary>
    /// Sorts the list. called automatically whenever modified
    /// </summary>
    public void Sort()
    {
        List.Sort(Comparater);
    }

    public List<T> GetList()
    {
        return new List<T>(List);
    }
    public IEnumerator<T> GetEnumerator()
    {
        return List.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    public void Add(T item)
    {
       List.Add(item);
       Sort();
    }
    public void Clear()
    {
       List.Clear();
    }
    public bool Contains(T item)
    {
        return List.Contains(item);
    }
    public void CopyTo(T[] array, int arrayIndex)
    {
         List.CopyTo(array, arrayIndex);
    }
    public bool Remove(T item)
    {
        bool result = List.Remove(item);
        if (result)
        {
            Sort();
        }
        return result;
    }
    public int Count => List.Count;
    public bool IsReadOnly => false;
    public int IndexOf(T item)
    {
        return List.IndexOf(item);
    }
    public void Insert(int index, T item)
    {
        List.Insert(index, item);
        Sort();
    }
    public void RemoveAt(int index)
    {
       List.RemoveAt(index);
       Sort();
    }
    public T this[int index]
    {
        get => List[index];
        set
        {
            List[index] = value;
            Sort();
        }
    }
}