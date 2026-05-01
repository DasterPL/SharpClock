using System;
using System.Collections.Generic;

namespace SharpClock
{
    class SettingsBuilder : ISettingsBuilder
    {
        readonly List<ISettingsEntry> _entries = new List<ISettingsEntry>();
        public IReadOnlyList<ISettingsEntry> All => _entries;

        public ISettingsEntry Add<T>(string key, Func<T> get, Action<T> set)
        {
            var entry = new SettingsEntry(this, key, typeof(T), () => get(), v => set(Converter.To<T>(v)));
            _entries.Add(entry);
            return entry;
        }
    }
}
