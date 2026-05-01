using System;
using System.Collections.Generic;

namespace SharpClock
{
    public interface ISettingsEntry
    {
        string Key { get; }
        Type ValueType { get; }
        Func<object> Get { get; }
        Action<object> Set { get; }
        IReadOnlyDictionary<string, string> Labels { get; }
        IReadOnlyDictionary<string, string[]> EnumLabels { get; }
        Func<bool> Visibility { get; }
        double? Min { get; }
        double? Max { get; }
        double? Step { get; }
        bool IsPassword { get; }
        bool IsMultiline { get; }

        ISettingsEntry Label(string lang, string text);
        ISettingsEntry EnumLabel(string lang, params string[] values);
        ISettingsEntry Password();
        ISettingsEntry Multiline();
        ISettingsEntry Range(double min, double max);
        ISettingsEntry StepSize(double step);
        ISettingsEntry When(Func<bool> condition);
        ISettingsEntry Add<T>(string key, Func<T> get, Action<T> set);
    }
}
