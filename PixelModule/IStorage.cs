using System.Collections.Generic;

namespace SharpClock
{
    public interface IStorage
    {
        string Get(string key, string defaultValue = null);
        void Set(string key, string value);
        void Delete(string key);
        IReadOnlyDictionary<string, string> GetAll();
    }
}
