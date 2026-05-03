using System;
using System.Collections.Generic;

namespace SharpClock
{
    public abstract class PixelGlobalSettings
    {
        static readonly List<PixelGlobalSettings> _all = new List<PixelGlobalSettings>();
        public static IReadOnlyList<PixelGlobalSettings> All => _all.AsReadOnly();

        static Func<ISettingsBuilder> _settingsFactory;
        internal static void SetSettingsFactory(Func<ISettingsBuilder> factory) => _settingsFactory = factory;

        public string Name { get; }
        public ISettingsBuilder Settings { get; }

        protected PixelGlobalSettings(string name)
        {
            Name = name;
            Settings = _settingsFactory?.Invoke();
            _all.Add(this);
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
