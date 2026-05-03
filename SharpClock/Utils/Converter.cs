using System;
using System.Drawing;
using System.Globalization;

namespace SharpClock
{
    static class Converter
    {
        public static T To<T>(object value)
        {
            if (value is T t) return t;
            var type = typeof(T);
            var s = value?.ToString() ?? "";
            if (type == typeof(string)) return (T)(object)s;
            if (type == typeof(int))    return (T)(object)int.Parse(s);
            if (type == typeof(float))  return (T)(object)float.Parse(s, CultureInfo.InvariantCulture);
            if (type == typeof(bool))   return (T)(object)bool.Parse(s);
            if (type == typeof(Color))  return (T)(object)ColorTranslator.FromHtml(s);
            if (type.IsEnum)            return (T)Enum.Parse(type, s);
            if (type == typeof(TimeSpan))
            {
                var parts = Array.ConvertAll(s.Split(':'), int.Parse);
                return (T)(object)new TimeSpan(parts[0], parts[1], parts.Length > 2 ? parts[2] : 0);
            }
            return (T)System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        public static string Serialize(Type type, object value)
        {
            if (type == typeof(Color))
            {
                var c = (Color)value;
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            if (type == typeof(float))
                return ((float)value).ToString(CultureInfo.InvariantCulture);
            return value?.ToString() ?? "";
        }
    }
}
