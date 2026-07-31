using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace SharpClock
{
    class PixelRenderer : IPixelRenderer
    {
        internal static IPixelRenderer Pixel { get; private set; }
        Thread drawThread;

        // `modules` is touched by the HTTP threads (load/unload/query), the GPIO thread
        // (settings menu) and the render thread. Every access goes through _modulesLock.
        readonly object _modulesLock = new object();
        List<PixelModule> modules = new List<PixelModule>();

        // Immutable snapshot the render loop iterates over. Published once per rotation so
        // that external callers (SwitchModule/PrevModule) can address the same index space.
        volatile PixelModule[] _order = new PixelModule[0];

        readonly object _reloadLock = new object();

        IPixelDraw Screen;

        // Cross-thread flags: ARMv6 has a weak memory model, these must not be cached in registers.
        volatile bool stop = false;
        volatile bool nextModule = false;
        volatile bool _manualNext = false;
        volatile bool _isRunning = false;
        volatile bool _rebuildOrder = false;
        volatile PixelModule _switchTarget;

        public bool IsReady { get; private set; } = false;
        volatile PixelModule _current;
        public PixelModule Current { get => _current; private set => _current = value; }
        public bool IsRunning => _isRunning;
        public bool Pause { get; set; } = false;
        public bool AnimatedSwitching { get; set; } = false;
        public bool RandomMode { get; set; } = false;
        public PixelModule[] GetModules { get { lock (_modulesLock) return modules.ToArray(); } }
        readonly Random _rng = new Random();

        /// <summary>How long a caller will wait for the render thread to acknowledge a module switch.</summary>
        const int SwitchTimeoutMs = 3000;

        internal PixelRenderer(IPixelDraw Screen)
        {
            if (Pixel == null)
            {
                Pixel = this;
            }
            else
            {
                throw new Exception("Can't create more Renderer");
            }
            this.Screen = Screen;
            GPIO.GPIOevents.OnButtonClick += GPIOevents_OnButtonClick;
        }

        private void GPIOevents_OnButtonClick(ButtonId button)
        {
            GPIO.GPIOevents.EnableBuzzer(1, 20, 0);
            Logger.Log(ConsoleColor.DarkMagenta, "Button: ", ConsoleColor.White, button.ToString());
            switch (button)
            {
                case ButtonId.Next:
                    NextModule();
                    break;
                case ButtonId.LongNext:
                    bool alreadyOpen;
                    lock (_modulesLock) alreadyOpen = modules.Any(m => m is SettingModule);
                    if (alreadyOpen) break;
                    SettingModule settingModule = new SettingModule();
                    lock (_modulesLock) modules.Add(settingModule);
                    _rebuildOrder = true;
                    settingModule.Start(Program.UpTime);
                    SwitchModule(settingModule, true);
                    new Thread(() => WatchSettingModule(settingModule)) { IsBackground = true }.Start();
                    break;
                case ButtonId.Pause:
                    Pause = !Pause;
                    break;
                case ButtonId.User1:
                case ButtonId.User2:
                case ButtonId.User3:
                    Current?.OnButtonClick(button);
                    break;
            }
        }

        /// <summary>
        /// Keeps the transient settings menu alive only while it is the module on screen,
        /// then tears it down. Bounded so a failed switch cannot spin forever.
        /// </summary>
        void WatchSettingModule(SettingModule settingModule)
        {
            try
            {
                var appearBy = DateTime.UtcNow.AddSeconds(5);
                while (settingModule.IsRunning && Current != settingModule && DateTime.UtcNow < appearBy)
                    Thread.Sleep(50);

                while (settingModule.IsRunning)
                {
                    if (Current != settingModule)
                        settingModule.Stop();
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, $"[SettingModule] watcher: {e.Message}");
            }
            finally
            {
                lock (_modulesLock) modules.Remove(settingModule);
                _rebuildOrder = true;
            }
        }

        public void LoadModule(string absolutePath)
        {
            try
            {
                Assembly dll = Assembly.LoadFile(absolutePath);

                // Diff by identity, not by index: reloading a plugin replaces registrations
                // in place, so the collection size is not a reliable "what's new" signal.
                var globalsBefore = new HashSet<PixelGlobalSettings>(PixelGlobalSettings.All);
                foreach (Type type in dll.GetExportedTypes())
                {
                    if (!typeof(PixelGlobalSettings).IsAssignableFrom(type) || type.IsAbstract) continue;
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                foreach (var gs in PixelGlobalSettings.All.Where(g => !globalsBefore.Contains(g)))
                {
                    gs.Load(Config.GetGlobalParams(gs.Name));
                    Logger.Log(ConsoleColor.Green, $"[GlobalSettings] Loaded: {gs.Name}");
                }

                var servicesBefore = new HashSet<PixelService>(PixelService.All);
                foreach (Type type in dll.GetExportedTypes())
                {
                    if (!typeof(PixelService).IsAssignableFrom(type) || type.IsAbstract) continue;
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                foreach (var svc in PixelService.All.Where(s => !servicesBefore.Contains(s)))
                {
                    svc.Start();
                    Logger.Log(ConsoleColor.Green, $"[Service] Started: {svc.Name}");
                }

                foreach (Type type in dll.GetExportedTypes())
                {
                    if (!typeof(PixelModule).IsAssignableFrom(type) || type.IsAbstract) continue;
                    try
                    {
                        PixelModule tmp = (PixelModule)Activator.CreateInstance(type);
                        Logger.Log(ConsoleColor.Blue, $"[{type.Name}]:", ConsoleColor.White, "Loading module");
                        var moduleCfg = Config.GetModule(type.Name);
                        if (moduleCfg == null)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{type.Name}]", ConsoleColor.Cyan, " Config not found, creating new");
                            Config.CreateModule(tmp);
                        }
                        else
                        {
                            foreach (var entry in tmp.Settings.All)
                            {
                                if (!moduleCfg.Params.TryGetValue(entry.Key, out string savedValue))
                                    continue;
                                try
                                {
                                    entry.Set(savedValue);
                                    Logger.Log(ConsoleColor.DarkMagenta, "Setting: ", ConsoleColor.White, $"{entry.Key} = {entry.Get()}, Type: {entry.ValueType.Name}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(ConsoleColor.Blue, $"[{tmp.Name}]:", ConsoleColor.Red, $"[Skip] Failed to set {entry.Key} = \"{savedValue}\": {ex.Message}");
                                }
                            }
                        }

                        if (moduleCfg == null)
                            moduleCfg = Config.GetModule(type.Name);

                        if (moduleCfg?.Start == true)
                        {
                            tmp.Start(Program.UpTime);
                        }

                        PixelModule replaced;
                        lock (_modulesLock)
                        {
                            replaced = modules.Find(m => m.Name == tmp.Name);
                            if (replaced != null) modules.Remove(replaced);
                            modules.Add(tmp);
                        }
                        _rebuildOrder = true;
                        if (replaced != null)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{tmp.Name}]:", ConsoleColor.DarkYellow, "exist - updateing!");
                            replaced.Stop();
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log(ConsoleColor.Blue, $"[{type.Name}]:", ConsoleColor.Red, $"[Error] Failed to load module: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, $"Failed to load assembly: {e.Message}");
            }
        }
        public void Start()
        {
            lock (_modulesLock) modules.Clear();

            string modulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");
            if (Directory.Exists(modulesDir))
            {
                foreach (var absolutePath in Directory.GetFiles(modulesDir, "*.dll"))
                {
                    Logger.Log(ConsoleColor.Green, $"Loading: {Path.GetFileName(absolutePath)}");
                    LoadModule(absolutePath);
                }
            }
            IsReady = true;

            Screen.Brightness = Config.Brightness;
            AnimatedSwitching = Config.AnimatedSwitching;
            RandomMode = Config.RandomMode;
            Logger.Log(ConsoleColor.Cyan, "System Ready!");
            drawThread = new Thread(Render) { Name = "render" };
            LoadingAnimation.Stop();
            drawThread.Start();
        }
        public void Stop()
        {
            Logger.Log("Stopping Renderer");
            stop = true;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (_isRunning && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (_isRunning) Logger.Log(ConsoleColor.Red, "Renderer did not stop in time, forcing shutdown");
            Logger.Log("Stopping modules");
            PixelModule[] snapshot;
            lock (_modulesLock) snapshot = modules.ToArray();
            foreach (var module in snapshot)
            {
                try { module.Stop(); }
                catch (Exception e) { Logger.Log(ConsoleColor.Blue, $"[{module.Name}]:", ConsoleColor.Red, $"[Stop] {e.Message}"); }
            }
            Logger.Log("Clearing Screen");
            Screen.Clear();
            Screen.Draw();
        }
        public void Reload()
        {
            // Two concurrent /modules/order calls used to be able to start two render threads.
            lock (_reloadLock)
            {
                stop = true;
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (_isRunning && DateTime.UtcNow < deadline) Thread.Sleep(10);
                if (_isRunning)
                {
                    Logger.Log(ConsoleColor.Red, "Reload aborted: render thread did not stop");
                    return;
                }
                drawThread = new Thread(Render) { Name = "render" };
                drawThread.Start();
            }
        }
        string[] moduleOrder;
        volatile int currentModuleNumber = 0;
        readonly NullModule _nullModule = new NullModule();
        readonly Stopwatch _nullTimer = Stopwatch.StartNew();

        int OrderRank(PixelModule m)
        {
            int i = Array.IndexOf(moduleOrder, m.Name);
            return i < 0 ? int.MaxValue : i; // modules missing from config sort to the end
        }

        /// <summary>Snapshot of the module list in display order, taken once per rotation.</summary>
        PixelModule[] BuildOrder()
        {
            PixelModule[] snapshot;
            lock (_modulesLock) snapshot = modules.ToArray();

            if (RandomMode)
            {
                for (int i = snapshot.Length - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    var tmp = snapshot[i]; snapshot[i] = snapshot[j]; snapshot[j] = tmp;
                }
                return snapshot;
            }

            return snapshot.OrderBy(OrderRank).ToArray(); // OrderBy is stable, Array.Sort is not
        }

        static bool Displayable(PixelModule m, bool manualNext) =>
            m.IsRunning && m.Visible && (!m.ExcludeFromQueue || manualNext);

        /// <summary>Allocation-free equivalent of order.Any(m => Displayable(m, false)) — runs per frame while idle.</summary>
        static bool AnyDisplayable(PixelModule[] order)
        {
            for (int i = 0; i < order.Length; i++)
                if (Displayable(order[i], false)) return true;
            return false;
        }

        void Render()
        {
            stop = false;
            _isRunning = true;

            moduleOrder = Config.ModuleOrder;
            try
            {
                while (!stop)
                {
                    // Cleared first: a change arriving while we build must trigger another pass.
                    _rebuildOrder = false;
                    var order = BuildOrder();
                    _order = order;

                    // Screensaver: hold here while nothing is displayable, re-checking every
                    // frame. Kept as an inner loop so we do not rebuild the snapshot 30×/s.
                    while (!stop && !_rebuildOrder && _switchTarget == null && !_manualNext
                           && !AnyDisplayable(order))
                    {
                        int idleStart = (int)_nullTimer.ElapsedMilliseconds;
                        Screen.Clear();
                        _nullModule.Draw(_nullTimer);
                        Screen.Draw();
                        int idleLeft = 33 - (int)(_nullTimer.ElapsedMilliseconds - idleStart);
                        Thread.Sleep(idleLeft > 0 ? idleLeft : 0);
                    }
                    if (stop) break;
                    if (_rebuildOrder) continue;

                    bool displayedAny = false;
                    for (currentModuleNumber = 0; currentModuleNumber < order.Length; currentModuleNumber++)
                    {
                        if (stop || _rebuildOrder)
                            break;
                        if (currentModuleNumber < 0)
                            currentModuleNumber = 0; // NextModule may steer us to -1

                        // Honour a pending SwitchModule request. The render thread is the only
                        // writer that clears it, so the requester can await it without a lock.
                        var target = _switchTarget;
                        if (target != null)
                        {
                            int ti = Array.IndexOf(order, target);
                            if (ti < 0)
                            {
                                // Just added (e.g. the settings menu) and not in this snapshot yet.
                                _rebuildOrder = true;
                                Thread.Sleep(20);
                                break;
                            }
                            currentModuleNumber = ti;
                            nextModule = false;
                            _manualNext = true; // a targeted switch may address an ExcludeFromQueue module
                            _switchTarget = null;
                        }

                        Current = order[currentModuleNumber];

                        if (nextModule || !Displayable(Current, _manualNext))
                        {
                            nextModule = false;
                            continue;
                        }
                        _manualNext = false;
                        displayedAny = true;
                        var timer = Stopwatch.StartNew();
                        while (Pause || timer.ElapsedMilliseconds < Current.Timer)
                        {
                            int start = (int)timer.ElapsedMilliseconds;
                            if (stop || nextModule || !Current.IsRunning || !Current.Visible)
                            {
                                nextModule = false;
                                break;
                            }
                            Screen.Clear();
                            try { Current.Draw(timer); }
                            catch (Exception ex)
                            {
                                Logger.Log(ConsoleColor.Blue, $"[{Current.Name}]:", ConsoleColor.Red, $"[Draw Error] {ex.Message}");
                                nextModule = true;
                            }
                            Screen.Draw();
                            int end = (int)timer.ElapsedMilliseconds;
                            int elapsed = HardwareConfig.FrameMs - (end - start);
                            Thread.Sleep(elapsed > 0 ? elapsed : 0);
                        }

                        // Skip the slide if a targeted switch is already queued, otherwise we
                        // animate into the sequentially-next module and immediately jump away.
                        if (!stop && AnimatedSwitching && _switchTarget == null)
                            AnimateTo(order, timer);
                    }

                    if (!displayedAny && !stop && !_rebuildOrder)
                    {
                        // A whole rotation with nothing displayable (everything hidden or stopped).
                        // Release the manual-advance latch, otherwise the screensaver guard above
                        // stays disarmed and this loop spins without ever sleeping.
                        _manualNext = false;
                        nextModule = false;
                        Thread.Sleep(33);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, $"[FATAL] render loop died: {e}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Slides from the frame currently on screen into the next displayable module.
        /// The scan is bounded: if nothing else is displayable we simply skip the animation
        /// instead of spinning the render thread at 100% CPU forever.
        /// </summary>
        void AnimateTo(PixelModule[] order, Stopwatch timer)
        {
            int nextModuleNr = -1;
            for (int step = 1; step <= order.Length; step++)
            {
                int candidate = (currentModuleNumber + step) % order.Length;
                if (candidate != currentModuleNumber && Displayable(order[candidate], _manualNext))
                {
                    nextModuleNr = candidate;
                    break;
                }
            }
            if (nextModuleNr < 0) return;

            var currentBuffer = Screen.GetBuffer();

            Screen.Clear();
            try { order[nextModuleNr].Draw(timer); }
            catch (Exception ex)
            {
                Logger.Log(ConsoleColor.Blue, $"[{order[nextModuleNr].Name}]:", ConsoleColor.Red, $"[Draw Error] {ex.Message}");
            }
            var nextBuffer = Screen.GetBuffer();

            var animTimer = Stopwatch.StartNew();
            for (int yOff = 1; yOff <= 8; yOff++)
            {
                if (stop) return;
                int start = (int)animTimer.ElapsedMilliseconds;
                Screen.DrawFromBuffers(currentBuffer, nextBuffer, yOff);
                int elapsed = 33 - (int)(animTimer.ElapsedMilliseconds - start);
                Thread.Sleep(elapsed > 0 ? elapsed : 0);
            }
        }

        /// <summary>
        /// Asks the render loop to advance. Bounded: if the render thread is stopped or wedged
        /// the caller gives up instead of parking an HTTP/GPIO thread forever.
        /// </summary>
        public void NextModule()
        {
            _manualNext = true;
            nextModule = true;
            var deadline = DateTime.UtcNow.AddMilliseconds(SwitchTimeoutMs);
            while (nextModule && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (nextModule)
            {
                nextModule = false;
                _manualNext = false;
                Logger.Log(ConsoleColor.Red, "[Renderer] NextModule timed out (render thread not responding)");
            }
        }
        public void PrevModule()
        {
            var order = _order;
            int count = order.Length;
            if (count == 0) return;
            int start = Array.IndexOf(order, Current);
            if (start < 0) start = 0;
            for (int i = 1; i <= count; i++)
            {
                int idx = ((start - i) % count + count) % count;
                if (order[idx].IsRunning && order[idx].Visible)
                {
                    SwitchModule(order[idx]);
                    return;
                }
            }
        }
        public bool SwitchModule(PixelModule module, bool forcePause = false)
        {
            if (module == null || !module.IsRunning)
                return false;
            if (forcePause)
                Pause = true;
            if (Current == module)
                return true;

            _switchTarget = module;
            _rebuildOrder = true; // the module may have been added after the current snapshot
            nextModule = true;    // cut the current module's dwell short

            var deadline = DateTime.UtcNow.AddMilliseconds(SwitchTimeoutMs);
            while (_switchTarget == module && DateTime.UtcNow < deadline) Thread.Sleep(10);

            if (_switchTarget == module)
            {
                _switchTarget = null;
                Logger.Log(ConsoleColor.Red, $"[Renderer] SwitchModule({module.Name}) timed out");
                return false;
            }
            return true;
        }
        public PixelModule GetModule(string name)
        {
            lock (_modulesLock) return modules.Find(x => x.Name == name);
        }
        public void UnloadModule(string dllFileName)
        {
            PixelService.UnloadFrom(dllFileName);
            List<PixelModule> toRemove;
            lock (_modulesLock)
            {
                toRemove = modules
                    .Where(m => Path.GetFileName(m.GetType().Assembly.Location) == dllFileName)
                    .ToList();
                foreach (var m in toRemove)
                    modules.Remove(m);
            }
            _rebuildOrder = true;
            foreach (var m in toRemove)
            {
                if (m.IsRunning) m.Stop();
                if (Current == m) NextModule();
                Logger.Log(ConsoleColor.Blue, $"[{m.Name}]:", ConsoleColor.White, "Unloaded");
            }
        }
        public void UpdateConfig(PixelModule module)
        {
            Config.EditModule(module);
        }
    }
}
