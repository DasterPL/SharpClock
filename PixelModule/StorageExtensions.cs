namespace SharpClock
{
    public static class StorageExtensions
    {
        public static T Get<T>(this IStorage storage, string key, T defaultValue = default)
        {
            string val = storage.Get(key);
            if (val == null) return defaultValue;
            try { return SettingsBuilder.Convert<T>(val); }
            catch { return defaultValue; }
        }

        public static void Set<T>(this IStorage storage, string key, T value) =>
            storage.Set(key, SettingsBuilder.Serialize(typeof(T), value));
    }
}
