using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    static class LoadingAnimation
    {
        static bool stop = false;
        static bool isStoped = false;
        internal static void Stop()
        {
            stop = true;
            while (!isStoped) Thread.Sleep(10);
        }
        static Point NextPerimeter(Point p)
        {
            if (p.Y == 0 && p.X < 31)       p.X++;
            else if (p.X == 31 && p.Y < 7)  p.Y++;
            else if (p.Y == 7 && p.X > 0)   p.X--;
            else if (p.X == 0 && p.Y > 0)   p.Y--;
            return p;
        }

        internal static void Run(IPixelDraw Screen)
        {
            new Thread(() =>
            {
                var loader = new Point(0, 0);
                var clear = new Point(3, 7);
                while (!stop)
                {
                    Screen.SetPixel(loader, Color.Aqua);
                    Screen.SetPixel(clear, Color.Black);
                    Screen.Draw();
                    Thread.Sleep(20);

                    loader = NextPerimeter(loader);
                    clear = NextPerimeter(clear);
                }
                GPIO.GPIOevents.EnableBuzzer(1,50,0);
                Screen.Clear();
                Screen.Draw();
                isStoped = true;
            }).Start();
        }
    }
}
