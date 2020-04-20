using System;

namespace SharpClock
{
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = true)]
    public class VisibleNameEnum : Attribute
    {
        public string[] values;
        public string lang;
    }
}
