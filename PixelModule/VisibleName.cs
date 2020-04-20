using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpClock
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class VisibleName : Attribute
    {
        public string value;
        public string lang;
    }
}
