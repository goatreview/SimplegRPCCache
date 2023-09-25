namespace Common;

public interface IKeyValueStore
{
    (bool,byte[]?) GetKey(string key);
    int SetKey(string key, byte[]? value,bool justRemove = false);
    string[] ListKeys(string filter);

}