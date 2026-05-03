using System.Collections.Generic;

namespace SharpClock
{
    class ModuleConfig
    {
        public string Name   { get; }
        public bool   Start  { get; }
        public Dictionary<string, string> Params { get; }

        public ModuleConfig(string name, bool start, Dictionary<string, string> parms)
        {
            Name   = name;
            Start  = start;
            Params = parms;
        }

        public override string ToString() => Name;
    }
}
