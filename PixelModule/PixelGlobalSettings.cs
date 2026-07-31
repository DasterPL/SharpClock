using System;
using System.Collections.Generic;

namespace SharpClock
{
    public abstract class PixelGlobalSettings
    {
        static readonly object _allLock = new object();
        static readonly List<PixelGlobalSettings> _all = new List<PixelGlobalSettings>();

        /// <summary>Snapshot — safe to enumerate while plugins are being loaded.</summary>
        public static IReadOnlyList<PixelGlobalSettings> All
        {
            get { lock (_allLock) return _all.ToArray(); }
        }

        static Func<ISettingsBuilder> _settingsFactory;
        internal static void SetSettingsFactory(Func<ISettingsBuilder> factory) => _settingsFactory = factory;

        public string Name { get; }
        public ISettingsBuilder Settings { get; }

        protected PixelGlobalSettings(string name)
        {
            Name = name;
            Settings = _settingsFactory?.Invoke();
            lock (_allLock)
            {
                // Reloading a plugin re-runs its static ctors; replace rather than duplicate.
                _all.RemoveAll(g => g.Name == name);
                _all.Add(this);
            }
        }

        internal void Load(Dictionary<string, string> savedParams)
        {
            foreach (var entry in Settings.All)
            {
                if (!savedParams.TryGetValue(entry.Key, out string val)) continue;
                try { entry.Set(val); }
                catch { }
            }
        }
    }
}
