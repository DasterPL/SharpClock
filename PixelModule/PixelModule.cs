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
        static Func<string, IStorage> _storageFactory;
        static Func<ISettingsBuilder> _settingsFactory;

        public static void SetScreen(IPixelDraw screen) => Screen = screen;
        public static void SetGPIO(IGPIO gpio) => GPIOevents = gpio;
        public static void SetRenderer(IPixelRenderer pixelRenderer) => PixelModule.pixelRenderer = pixelRenderer;
        public static void SetLogger(ILogger logger) => Logger._impl = logger;
        public static void SetStorageFactory(Func<string, IStorage> factory) => _storageFactory = factory;
        public static void SetSettingsFactory(Func<ISettingsBuilder> factory) => _settingsFactory = factory;

        Stopwatch Stopwatch;

        public string Name { get => GetType().Name; }
        public string Icon { get; set; } = "view_module";
        protected IStorage Storage { get; private set; }
        public bool Visible { get; set; } = true;
        public int Timer { get; set; } = 10000;
        protected int Tickrate { get; set; } = 1000;
        public bool IsRunning { get; private set; } = false;
        CancellationTokenSource tokenSource;

        public ISettingsBuilder Settings { get; private set; }

        protected PixelModule()
        {
            Storage = _storageFactory?.Invoke(GetType().Name);
            Settings = _settingsFactory?.Invoke();
            Settings
                .Add(nameof(Visible), () => Visible, v => Visible = v)
                    .Label("pl", "Widoczny").Label("en", "Visible")
                .Add(nameof(Timer), () => Timer, v => Timer = v)
                    .Label("pl", "Czas wyświetlania").Label("en", "Display time");
        }

        public virtual System.Collections.Generic.IDictionary<string, object> GetState() => null;

        public virtual void OnButtonClick(ButtonId button)
        {
            Logger.Log(ConsoleColor.DarkMagenta, $"[{Name}]:", ConsoleColor.White, $"Button: {button}");
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
                        try
                        {
                            Update(stopwatch);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Red, $"[Unhandled] {e.GetType().Name}: {e.Message}");
                        }
                        long end = stopwatch.ElapsedMilliseconds;
                        try
                        {
                            int sleepMs = Math.Max(0, Tickrate - (int)(end - start));
                            await Task.Delay(sleepMs, tokenSource.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Stopped");
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
                while (IsRunning) Thread.Sleep(10);
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
