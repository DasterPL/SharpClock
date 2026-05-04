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
        List<PixelModule> modules = new List<PixelModule>();
        IPixelDraw Screen;
        bool stop = false;
        bool nextModule = false;
        volatile bool _manualNext = false;

        public bool IsReady { get; private set; } = false;
        public PixelModule Current { get; private set; }
        public bool IsRunning { get; private set; } = false;
        public bool Pause { get; set; } = false;
        public bool AnimatedSwitching { get; set; } = false;
        public PixelModule[] GetModules { get => modules.ToArray(); }

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
                    if (modules.Any(m => m is SettingModule)) break;
                    SettingModule settingModule = new SettingModule();
                    modules.Add(settingModule);
                    settingModule.Start(Program.UpTime);
                    SwitchModule(settingModule, true);
                    new Thread(() =>
                    {
                        while (settingModule.IsRunning && Current != settingModule)
                            Thread.Sleep(50);
                        while (settingModule.IsRunning)
                        {
                            if (Current != settingModule)
                                settingModule.Stop();
                            Thread.Sleep(100);
                        }
                        modules.Remove(settingModule);
                    }) { IsBackground = true }.Start();
                    break;
                case ButtonId.Pause:
                    Pause = !Pause;
                    break;
                case ButtonId.User1:
                    Current.OnButtonClick(ButtonId.User1);
                    break;
                case ButtonId.User2:
                    Current.OnButtonClick(ButtonId.User2);
                    break;
                case ButtonId.User3:
                    Current.OnButtonClick(ButtonId.User3);
                    break;
            }
        }
        public void LoadModule(string absolutePath)
        {
            try
            {
                Assembly dll = Assembly.LoadFile(absolutePath);

                int prevGlobalCount = PixelGlobalSettings.All.Count;
                foreach (Type type in dll.GetExportedTypes())
                {
                    if (!typeof(PixelGlobalSettings).IsAssignableFrom(type) || type.IsAbstract) continue;
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                for (int i = prevGlobalCount; i < PixelGlobalSettings.All.Count; i++)
                {
                    var gs = PixelGlobalSettings.All[i];
                    gs.Load(Config.GetGlobalParams(gs.Name));
                    Logger.Log(ConsoleColor.Green, $"[GlobalSettings] Loaded: {gs.Name}");
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
                        if (modules.Any(m => m.Name == tmp.Name))
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{tmp.Name}]:", ConsoleColor.DarkYellow, "exist - updateing!");
                            var module = modules.Find(m => m.Name == tmp.Name);
                            module.Stop();
                            modules.Remove(module);
                        }
                        modules.Add(tmp);
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
            modules.Clear();

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
            Logger.Log(ConsoleColor.Cyan, "System Ready!");
            drawThread = new Thread(Render);
            LoadingAnimation.Stop();
            drawThread.Start();
        }
        public void Stop()
        {
            Logger.Log("Stopping Renderer");
            stop = true;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (IsRunning && DateTime.UtcNow < deadline) Thread.Sleep(10);
            if (IsRunning) Logger.Log(ConsoleColor.Red, "Renderer did not stop in time, forcing shutdown");
            Logger.Log("Stopping modules");
            foreach (var module in modules)
            {
                module.Stop();
            }
            Logger.Log("Clearing Screen");
            Screen.Clear();
            Screen.Draw();
        }
        public void Reload()
        {
            stop = true;
            while (IsRunning) Thread.Sleep(10);
            drawThread = new Thread(Render);
            drawThread.Start();
        }
        string[] moduleOrder;
        int currentModuleNumber = 0;
        readonly NullModule _nullModule = new NullModule();
        readonly Stopwatch _nullTimer = Stopwatch.StartNew();
        void Render()
        {
            stop = false;
            IsRunning = true;

            moduleOrder = Config.ModuleOrder;
            modules = modules.OrderBy(m => Array.IndexOf(moduleOrder, m.Name)).ToList();
            while (!stop)
            {
                bool anyActive = _manualNext || modules.Any(m => m.IsRunning && m.Visible && !m.ExcludeFromQueue);
                if (!anyActive)
                {
                    int start = (int)_nullTimer.ElapsedMilliseconds;
                    Screen.Clear();
                    _nullModule.Draw(_nullTimer);
                    Screen.Draw();
                    int elapsed = 33 - (int)(_nullTimer.ElapsedMilliseconds - start);
                    Thread.Sleep(elapsed > 0 ? elapsed : 0);
                    continue;
                }

                for (currentModuleNumber = 0; currentModuleNumber < modules.Count; currentModuleNumber++)
                {
                    if (stop)
                        break;

                    Current = modules[currentModuleNumber];

                    if (nextModule || !Current.IsRunning || !Current.Visible || (Current.ExcludeFromQueue && !_manualNext))
                    {
                        nextModule = false;
                        continue;
                    }
                    _manualNext = false;
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
                        int delay = elapsed > 0 ? elapsed : 0;
                        Thread.Sleep(delay);
                    }
                    if (!stop && AnimatedSwitching && modules.Any(m => m.IsRunning && m.Visible))
                    {
                        var currentBuffer = Screen.GetBuffer();

                        int nextModuleNr = currentModuleNumber + 1 >= modules.Count ? 0 : currentModuleNumber + 1;
                        while (!modules[nextModuleNr].IsRunning || !modules[nextModuleNr].Visible || (modules[nextModuleNr].ExcludeFromQueue && !_manualNext))
                            nextModuleNr = nextModuleNr >= modules.Count - 1 ? 0 : nextModuleNr + 1;

                        Screen.Clear();
                        try { modules[nextModuleNr].Draw(timer); }
                        catch (Exception ex)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{modules[nextModuleNr].Name}]:", ConsoleColor.Red, $"[Draw Error] {ex.Message}");
                        }
                        var nextBuffer = Screen.GetBuffer();

                        var animTimer = Stopwatch.StartNew();
                        for (int yOff = 1; yOff <= 8; yOff++)
                        {
                            int start = (int)animTimer.ElapsedMilliseconds;
                            Screen.DrawFromBuffers(currentBuffer, nextBuffer, yOff);
                            int elapsed = 33 - (int)(animTimer.ElapsedMilliseconds - start);
                            Thread.Sleep(elapsed > 0 ? elapsed : 0);
                        }
                    }
                }
            }
            IsRunning = false;
        }
        public void NextModule()
        {
            _manualNext = true;
            nextModule = true;
            while (nextModule) Thread.Sleep(10);
        }
        public bool SwitchModule(PixelModule module, bool forcePause = false)
        {
            if (!module.IsRunning)
                return false;
            if (forcePause)
                Pause = true;
            if (Current == module)
                return true;
            currentModuleNumber = modules.IndexOf(module) - 1;
            NextModule();
            return true;
        }
        public PixelModule GetModule(string name)
        {
            return modules.Find(x => x.Name == name);
        }
        public void UnloadModule(string dllFileName)
        {
            var toRemove = modules
                .Where(m => Path.GetFileName(m.GetType().Assembly.Location) == dllFileName)
                .ToList();
            foreach (var m in toRemove)
            {
                if (m.IsRunning) m.Stop();
                if (Current == m) NextModule();
                modules.Remove(m);
                Logger.Log(ConsoleColor.Blue, $"[{m.Name}]:", ConsoleColor.White, "Unloaded");
            }
        }
        public void UpdateConfig(PixelModule module)
        {
            Config.EditModule(module);
        }
    }
}
