using System;

namespace SharpClock
{
    [AttributeUsage(AttributeTargets.Property)]
    public class RangeAttribute : Attribute
    {
        public double Min { get; }
        public double Max { get; }
        public RangeAttribute(double min, double max) { Min = min; Max = max; }
    }
}
