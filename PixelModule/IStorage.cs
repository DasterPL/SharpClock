using System.Collections.Generic;

namespace SharpClock
{
    public abstract class Storage
    {
        public abstract string Get(string key, string defaultValue = null);
        public abstract void Set(string key, string value);
        public abstract void Delete(string key);
        public abstract IReadOnlyDictionary<string, string> GetAll();

        public T Get<T>(string key, T defaultValue = default)
        {
            string val = Get(key);
            if (val == null) return defaultValue;
            try { return SettingsBuilder.Convert<T>(val); }
            catch { return defaultValue; }
        }

        public void Set<T>(string key, T value) =>
            Set(key, SettingsBuilder.Serialize(typeof(T), value));
    }
}
