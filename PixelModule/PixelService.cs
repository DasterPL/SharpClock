using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer; // disambiguate from System.Threading.Timer

namespace SharpClock
{
    public abstract class PixelService
    {
        static readonly object _allLock = new object();
        static readonly List<PixelService> _all = new List<PixelService>();

        /// <summary>Snapshot — safe to enumerate while plugins are being loaded or unloaded.</summary>
        public static IReadOnlyList<PixelService> All
        {
            get { lock (_allLock) return _all.ToArray(); }
        }

        public string   Name      => GetType().Name;
        public bool     IsRunning { get; private set; } = false;
        public DateTime LastRun   { get; private set; } = DateTime.MinValue;
        public string   LastError { get; private set; }

        protected abstract int IntervalMs { get; }
        protected abstract void Run();

        Timer _timer;
        readonly string _dllName;
        int _inFlight; // guards against overlapping runs when Run() outlasts the interval

        protected PixelService()
        {
            _dllName = System.IO.Path.GetFileName(GetType().Assembly.Location);
            PixelService stale = null;
            lock (_allLock)
            {
                // Re-uploading a plugin re-runs the static ctors and produces a fresh instance.
                // Replace the old registration instead of accumulating duplicates that keep ticking.
                stale = _all.Find(s => s.Name == Name && s._dllName == _dllName);
                if (stale != null) _all.Remove(stale);
                _all.Add(this);
            }
            if (stale != null)
            {
                try { stale.Stop(); } catch { }
                Logger.Log(ConsoleColor.Blue, $"[{Name}]:", ConsoleColor.DarkYellow, "Service replaced");
            }
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
            var t = _timer;
            _timer = null;
            try { t?.Stop(); t?.Dispose(); } catch { }
        }

        internal static void UnloadFrom(string dllFileName)
        {
            List<PixelService> toRemove;
            lock (_allLock)
            {
                toRemove = _all.FindAll(s => s._dllName == dllFileName);
                foreach (var s in toRemove) _all.Remove(s);
            }
            foreach (var s in toRemove)
            {
                if (s.IsRunning) s.Stop();
                Logger.Log(ConsoleColor.Blue, $"[{s.Name}]:", ConsoleColor.White, "Service unloaded");
            }
        }

        void RunInternal()
        {
            if (!IsRunning) return;
            // A slow Run() (e.g. a weather fetch waiting out its timeout) must not stack up
            // behind the next timer tick.
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return;
            try
            {
                LastRun = DateTime.Now;
                Run();
                LastError = null;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Logger.Log(ConsoleColor.Blue, $"[{Name}]:", ConsoleColor.Red, e.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _inFlight, 0);
            }
        }
    }
}
