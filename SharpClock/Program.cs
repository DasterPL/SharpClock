using Mono.Unix;
using Mono.Unix.Native;
using System;
using System.Diagnostics;
using System.Threading;

namespace SharpClock
{
    class Program
    {
        public static Stopwatch UpTime;
        static HttpServer WebServer;
        static void Main(string[] args)
        {
            UpTime = Stopwatch.StartNew();
            
            Logger.Clear();
            Logger.Log("Sharp Clock v0.7.1");

            ParseCommands(args);
            HandleUnixSignals();

            var GPIOevents = new GPIO();
            var Screen = new PixelDraw();

            LoadingAnimation.Run(Screen);

            var Pixel = new PixelRenderer(PixelDraw.Screen);
            PixelModule.SetScreen(Screen);
            PixelModule.SetGPIO(GPIOevents);
            PixelModule.SetRenderer(Pixel);
            WebServer = new HttpServer("WebPage");
            WebServer.Start();

            Pixel.Start();
            
            PostHandler postHandler = new PostHandler(WebServer);
        }
        static void ParseCommands(string[] args)
        {
            if (args.Length > 0 && args[0] == "stop")
            {
                Process.Start("/bin/bash", "-c \"killall -s SIGUSR1 mono\"");
            }
        }
        public static void Kill()
        {
            Logger.Log();
            WebServer.Stop();
            LoadingAnimation.Stop();
            PixelRenderer.Pixel.Stop();
            Logger.Log("Shutingdown");
        }
        static void HandleUnixSignals()
        {
            //Handle UNIX signals
            UnixSignal[] signals = new UnixSignal[]
            {
                new UnixSignal(Signum.SIGINT),
                new UnixSignal(Signum.SIGUSR1),
            };
            new Thread(delegate ()
            {
                while (true)
                {
                    int index = UnixSignal.WaitAny(signals, -1);

                    Signum signal = signals[index].Signum;

                    if (signal == Signum.SIGINT || signal == Signum.SIGUSR1)
                    {
                        Kill();
                        Environment.Exit(0);
                    }
                }
            }).Start();
        }
    }
}

