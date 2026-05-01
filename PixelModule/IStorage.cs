using System.Collections.Generic;

namespace SharpClock
{
    public interface IStorage
    {
        T Get<T>(string key, T defaultValue = default);
        bool Set<T>(string key, T value);
        bool Delete(string key);
        IReadOnlyDictionary<string, object> GetAll();
    }

}
