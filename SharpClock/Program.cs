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
        public static ILogger AppLogger { get; private set; }
        static HttpServer WebServer;
        static void Main()
        {
            AppLogger = new FileLogger();
            PixelModule.SetLogger(AppLogger);
            PixelModule.SetSettingsFactory(() => new SettingsBuilder());
            PixelModule.SetStorageFactory(name => new Storage(name));
            PixelGlobalSettings.SetSettingsFactory(() => new SettingsBuilder());

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Logger.Log(ConsoleColor.Red, $"[FATAL] {e.ExceptionObject}");
                try { Kill(); } catch { }
            };

            UpTime = Stopwatch.StartNew();
            Logger.Log("Sharp Clock v1.0.0");

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
        static int _killed;
        public static void Kill()
        {
            // SIGTERM from systemd and a menu action can both land here; run the teardown once.
            if (Interlocked.Exchange(ref _killed, 1) != 0) return;
            try { WebServer?.Stop(); } catch (Exception e) { Logger.Log(ConsoleColor.Red, $"[Kill] WebServer: {e.Message}"); }
            try { LoadingAnimation.Stop(); } catch (Exception e) { Logger.Log(ConsoleColor.Red, $"[Kill] LoadingAnimation: {e.Message}"); }
            try { PixelRenderer.Pixel?.Stop(); } catch (Exception e) { Logger.Log(ConsoleColor.Red, $"[Kill] Renderer: {e.Message}"); }
            Logger.Log("Shutting down");
            (AppLogger as FileLogger)?.Flush();
        }
        static void HandleUnixSignals()
        {
            UnixSignal[] signals = new UnixSignal[]
            {
                new UnixSignal(Signum.SIGINT),
                new UnixSignal(Signum.SIGTERM),
                new UnixSignal(Signum.SIGUSR1),
            };
            new Thread(delegate ()
            {
                while (true)
                {
                    int index = UnixSignal.WaitAny(signals, -1);

                    Signum signal = signals[index].Signum;

                    if (signal == Signum.SIGINT || signal == Signum.SIGTERM || signal == Signum.SIGUSR1)
                    {
                        Kill();
                        Environment.Exit(0);
                    }
                }
            }).Start();
        }
    }
}
