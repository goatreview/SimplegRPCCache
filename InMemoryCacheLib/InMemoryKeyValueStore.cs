using System.Collections.Concurrent;
using Common;

namespace InMemoryCacheLib;

public class InMemoryKeyValueStore :IKeyValueStore
{
    
    private readonly ConcurrentDictionary<string,byte[]?> _store = new();
    public (bool,byte[]?) GetKey(string key)
    {
        return _store.TryGetValue(key, out byte[]? result) ? (false, result) : (true, null);
    }

    public string[] ListKeys(string filter)
    { 
        if (string.IsNullOrEmpty(filter))
            return _store.Keys.ToArray();
        
        return _store.Keys.Where(x => x.Contains(filter)).ToArray();
    }

    public int SetKey(string key, byte[]? value,bool justRemove = false)
    {
        if (justRemove)
        {
            if (_store.TryRemove(key, out _))
                return 0;
            else
                return -1;
        }
        _store.AddOrUpdate(key, value, (key, oldValue) => value);
        return 0;
    }
}