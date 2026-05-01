using System;
using System.Collections.Generic;

namespace SharpClock
{
    public interface ISettingsBuilder
    {
        IReadOnlyList<ISettingsEntry> All { get; }
        ISettingsEntry Add<T>(string key, Func<T> get, Action<T> set);
    }
}
