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
        public static void Stop() 
        { 
            stop = true;
            while (!isStoped) ;
        } 
        public static void Run(IPixelDraw Screen)
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

                    if (loader.Y == 0)
                        loader.X++;
                    else if (loader.Y == 7)
                        loader.X--;
                    if (loader.X == 31)
                        loader.Y++;
                    else if (loader.X == 0)
                        loader.Y--;

                    if (clear.Y == 0)
                        clear.X++;
                    else if (clear.Y == 7)
                        clear.X--;
                    if (clear.X == 31)
                        clear.Y++;
                    else if (clear.X == 0)
                        clear.Y--;
                }
                GPIO.GPIOevents.EnableBuzzer(1,50,0);
                Screen.Clear();
                Screen.Draw();
                isStoped = true;
            }).Start();
        }
    }
}
