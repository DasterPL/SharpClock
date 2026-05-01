using System;
using System.Collections.Generic;

namespace SharpClock
{
    class SettingsEntry : ISettingsEntry
    {
        readonly ISettingsBuilder _builder;
        public Func<object> Get { get; }
        public Action<object> Set { get; }
        public string Key { get; }
        public Type ValueType { get; }

        readonly Dictionary<string, string> _labels = new Dictionary<string, string>();
        readonly Dictionary<string, string[]> _enumLabels = new Dictionary<string, string[]>();

        public IReadOnlyDictionary<string, string> Labels => _labels;
        public IReadOnlyDictionary<string, string[]> EnumLabels => _enumLabels;
        public Func<bool> Visibility { get; private set; }
        public double? Min { get; private set; }
        public double? Max { get; private set; }
        public double? Step { get; private set; }
        public bool IsPassword { get; private set; }
        public bool IsMultiline { get; private set; }

        public SettingsEntry(ISettingsBuilder builder, string key, Type type, Func<object> get, Action<object> set)
        {
            _builder = builder;
            Key = key;
            ValueType = type;
            Get = get;
            Set = set;
        }

        public ISettingsEntry Label(string lang, string text) { _labels[lang] = text; return this; }
        public ISettingsEntry EnumLabel(string lang, params string[] values) { _enumLabels[lang] = values; return this; }
        public ISettingsEntry Password() { IsPassword = true; return this; }
        public ISettingsEntry Multiline() { IsMultiline = true; return this; }
        public ISettingsEntry Range(double min, double max) { Min = min; Max = max; return this; }
        public ISettingsEntry StepSize(double step) { Step = step; return this; }
        public ISettingsEntry When(Func<bool> condition) { Visibility = condition; return this; }
        public ISettingsEntry Add<T>(string key, Func<T> get, Action<T> set) => _builder.Add(key, get, set);
    }
}
