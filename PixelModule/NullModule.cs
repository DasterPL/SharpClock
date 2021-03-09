using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpClock
{
    public class NullModule : PixelModule
    {
        public override void Draw(Stopwatch stopwatch)
        {
            //do nothing
        }

        protected override void Update(Stopwatch stopwatch)
        {
            //do nothing
        }
    }
}
