using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    public abstract class PixelModule
    {
        static protected IPixelDraw Screen { get; private set; }
        static protected IGPIO GPIOevents { get; private set; }
        static protected IPixelRenderer pixelRenderer { get; private set; }
        public static void SetScreen(IPixelDraw screen)
        {
            Screen = screen;
        }
        public static void SetGPIO(IGPIO gpio)
        {
            GPIOevents = gpio;
        }
        public static void SetRenderer(IPixelRenderer pixelRenderer)
        {
            PixelModule.pixelRenderer = pixelRenderer;
        }

        Stopwatch Stopwatch;

        public string Name { get => GetType().Name; }
        public string Icon { get; set; } = "view_module";
        
        [VisibleName(lang = "pl", value = "Widoczny")]
        [VisibleName(lang = "en", value = "Visible")]
        public bool Visible { get; set; } = true;
        [VisibleName(lang = "pl", value = "Jak długo ma się wyświetlać w milisekundach")]
        [VisibleName(lang = "en", value = "How long it should display in miliseconds")]
        public int Timer { get; set; } = 10000;
        protected int Tickrate { get; set; } = 1000;
        public bool IsRunning { get; private set; } = false;
        CancellationTokenSource tokenSource;

        public virtual void OnButtonClick(ButtonId button)
        {
            Console.WriteLine($"Module: {Name} Button: {button.ToString()}");
        }

        public void Start(Stopwatch stopwatch)
        {
            this.Stopwatch = stopwatch;
            if (!IsRunning)
            {
                Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Started");
                tokenSource = new CancellationTokenSource();
                Task updateTask = new Task(async () =>
                {
                    IsRunning = true;
                    while (!tokenSource.Token.IsCancellationRequested)
                    {
                        long start = stopwatch.ElapsedMilliseconds;
                        Update(stopwatch);
                        long end = stopwatch.ElapsedMilliseconds;
                        try
                        {
                            await Task.Delay(Tickrate - (int)(end - start), tokenSource.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Stoped");
                        }
                    }
                    IsRunning = false;
                });
                updateTask.Start();
            }
            else
                Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.Red, "[Error]", ConsoleColor.White, " Can't start module again");
        }
        public void Stop()
        {
            if (IsRunning)
            {
                tokenSource.Cancel();
                while (IsRunning) ;
            }
        }
        public virtual void Reload()
        {
            Stop();
            Start(Stopwatch);
        }
        protected abstract void Update(Stopwatch stopwatch);
        public abstract void Draw(Stopwatch stopwatch);
    }
}
