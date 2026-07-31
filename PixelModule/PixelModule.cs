using System;
using System.Diagnostics;
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

        internal static void SetScreen(IPixelDraw screen) => Screen = screen;
        internal static void SetGPIO(IGPIO gpio) => GPIOevents = gpio;
        internal static void SetRenderer(IPixelRenderer pixelRenderer) => PixelModule.pixelRenderer = pixelRenderer;
        internal static void SetLogger(ILogger logger) => Logger._impl = logger;
        internal static void SetStorageFactory(Func<string, IStorage> factory) => _storageFactory = factory;
        internal static void SetSettingsFactory(Func<ISettingsBuilder> factory) => _settingsFactory = factory;

        Stopwatch Stopwatch;

        public string Name { get => GetType().Name; }
        public string Icon { get; set; } = "view_module";
        protected IStorage Storage { get; private set; }
        public bool Visible          { get; set; } = true;
        public bool ExcludeFromQueue { get; set; } = false;
        public int  Timer            { get; set; } = 10000;
        protected int Tickrate { get; set; } = 1000;
        volatile bool _isRunning = false;
        public bool IsRunning => _isRunning;
        CancellationTokenSource tokenSource;

        /// <summary>How long Stop() waits for the update loop before giving up and logging.</summary>
        const int StopTimeoutMs = 5000;

        public ISettingsBuilder Settings { get; private set; }

        protected PixelModule()
        {
            Storage = _storageFactory?.Invoke(GetType().Name);
            Settings = _settingsFactory?.Invoke();
            Settings
                .Add(nameof(Visible), () => Visible, v => Visible = v)
                    .Label("pl", "Widoczny").Label("en", "Visible")
                .Add(nameof(Timer), () => Timer, v => Timer = v)
                    .Label("pl", "Czas wyświetlania").Label("en", "Display time")
                .Add(nameof(ExcludeFromQueue), () => ExcludeFromQueue, v => ExcludeFromQueue = v)
                    .Label("pl", "Wyklucz z kolejki").Label("en", "Exclude from queue")
                    .When(() => false);
        }

        public virtual System.Collections.Generic.IDictionary<string, object> GetState() => null;

        public virtual void OnButtonClick(ButtonId button)
        {
            Logger.Log(ConsoleColor.DarkMagenta, $"[{Name}]:", ConsoleColor.White, $"Button: {button}");
        }

        public void Start(Stopwatch stopwatch)
        {
            this.Stopwatch = stopwatch;
            if (_isRunning)
            {
                Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.Red, "[Error]", ConsoleColor.White, " Can't start module again");
                return;
            }

            Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Started");
            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            // Published before the task starts, so callers that Start() then immediately
            // check IsRunning (LoadModule, SwitchModule) never see a stale false.
            _isRunning = true;

            if (Tickrate == int.MaxValue)
                return;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
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
                            await Task.Delay(sleepMs, token);
                        }
                        catch (TaskCanceledException)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.White, " Stopped");
                        }
                    }
                }
                finally
                {
                    // Must run on every exit path, otherwise Stop() would wait forever
                    // and the module would look alive while its loop is dead.
                    _isRunning = false;
                }
            });
        }
        public void Stop()
        {
            if (!_isRunning) return;

            tokenSource?.Cancel();
            if (Tickrate == int.MaxValue)
            {
                _isRunning = false;
            }
            else
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(StopTimeoutMs);
                while (_isRunning && DateTime.UtcNow < deadline) Thread.Sleep(10);
                if (_isRunning)
                {
                    // The loop is wedged (typically a blocking call inside Update). Stop waiting
                    // for it, but leave the token source alive — it is still in use by that loop.
                    Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Red,
                        " did not stop in time, abandoning update loop");
                    _isRunning = false;
                    tokenSource = null;
                    return;
                }
            }
            try { tokenSource?.Dispose(); } catch { }
            tokenSource = null;
        }
        public virtual void Reload()
        {
            Stop();
            Start(Stopwatch);
        }
        protected virtual void Update(Stopwatch stopwatch) { }
        public abstract void Draw(Stopwatch stopwatch);
    }
}
