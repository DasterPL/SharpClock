using System;
using System.Diagnostics;
using System.Drawing;

namespace SharpClock
{
    class NullModule : PixelModule
    {
        const int W = 32, H = 8, TrailLen = 4;

        static readonly Color[] Trail = {
            Color.FromArgb(0, 255, 80),
            Color.FromArgb(0, 180, 40),
            Color.FromArgb(0,  90, 20),
            Color.FromArgb(0,  35,  8),
        };

        readonly int[] _cyclePeriod  = new int[W];
        readonly int[] _dropDuration = new int[W];
        readonly int[] _phase        = new int[W];

        public NullModule()
        {
            Timer = int.MaxValue;
            for (int x = 0; x < W; x++)
            {
                var rng = new Random(x * 1664525 + 1013904223);
                int msPerPx = 90 + rng.Next(0, 70);
                _dropDuration[x] = (H + TrailLen) * msPerPx;
                _cyclePeriod[x]  = _dropDuration[x] + rng.Next(200, 900);
                _phase[x]        = rng.Next(0, _cyclePeriod[x]);
            }
        }

        public override void Draw(Stopwatch stopwatch)
        {
            long ms = stopwatch.ElapsedMilliseconds;
            for (int x = 0; x < W; x++)
            {
                long t = (ms + _phase[x]) % _cyclePeriod[x];
                if (t >= _dropDuration[x]) continue;
                int head = (int)(t * (H + TrailLen) / _dropDuration[x]) - TrailLen;
                for (int tr = 0; tr < TrailLen; tr++)
                {
                    int y = head - tr;
                    if (y >= 0 && y < H)
                        Screen.SetPixel(new Point(x, y), Trail[tr]);
                }
            }
        }

        protected override void Update(Stopwatch stopwatch) { }
    }
}
