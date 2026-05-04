using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace SharpClock
{
    public abstract class PixelService
    {
        static readonly List<PixelService> _all = new List<PixelService>();
        public static IReadOnlyList<PixelService> All => _all.AsReadOnly();

        public string   Name      => GetType().Name;
        public bool     IsRunning { get; private set; } = false;
        public DateTime LastRun   { get; private set; } = DateTime.MinValue;
        public string   LastError { get; private set; }

        protected abstract int IntervalMs { get; }
        protected abstract void Run();

        Timer _timer;
        string _dllName;

        protected PixelService()
        {
            _dllName = System.IO.Path.GetFileName(GetType().Assembly.Location);
            _all.Add(this);
        }

        internal void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _timer = new Timer(IntervalMs) { AutoReset = true };
            _timer.Elapsed += (s, e) => Task.Run((Action)RunInternal);
            _timer.Start();
            Task.Run((Action)RunInternal);
        }

        internal void Stop()
        {
            IsRunning = false;
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        internal static void UnloadFrom(string dllFileName)
        {
            var toRemove = _all.FindAll(s => s._dllName == dllFileName);
            foreach (var s in toRemove)
            {
                if (s.IsRunning) s.Stop();
                _all.Remove(s);
                Logger.Log(ConsoleColor.Blue, $"[{s.Name}]:", ConsoleColor.White, "Service unloaded");
            }
        }

        void RunInternal()
        {
            LastRun = DateTime.Now;
            try
            {
                Run();
                LastError = null;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Logger.Log(ConsoleColor.Blue, $"[{Name}]:", ConsoleColor.Red, e.Message);
            }
        }
    }
}
