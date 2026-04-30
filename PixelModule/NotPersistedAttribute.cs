using System;

namespace SharpClock
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NotPersistedAttribute : Attribute { }
}
