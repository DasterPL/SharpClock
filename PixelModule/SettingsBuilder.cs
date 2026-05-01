using System;
using System.Collections.Generic;

namespace SharpClock
{
    public class SettingsBuilder
    {
        readonly List<SettingsEntry> _entries = new List<SettingsEntry>();
        public IReadOnlyList<SettingsEntry> All => _entries;

        public SettingsEntry Add<T>(string key, Func<T> get, Action<T> set)
        {
            var entry = new SettingsEntry(this, key, typeof(T), () => get(), v => set(Converter.To<T>(v)));
            _entries.Add(entry);
            return entry;
        }
    }
}
