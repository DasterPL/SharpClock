using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    class PixelRenderer : IPixelRenderer
    {
        public static PixelRenderer Pixel { get; private set; }
        Thread drawThread;
        List<PixelModule> modules = new List<PixelModule>();
        IPixelDraw Screen;
        bool stop = false;
        bool nextModule = false;
        //Config Config = new Config();

        public bool IsReady { get; private set; } = false;
        public PixelModule Current { get; private set; }
        public bool IsRunning { get; private set; } = false;
        public bool Pause { get; set; } = false;
        public PixelModule[] GetModules { get => modules.ToArray(); }

        public PixelRenderer(IPixelDraw Screen)
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
            Console.WriteLine(button);
            switch (button)
            {
                case ButtonId.Next:
                    NextModule();
                    break;
                case ButtonId.LongNext:
                    SettingModule settingModule = new SettingModule();
                    modules.Add(settingModule);
                    settingModule.Start(Program.UpTime);
                    SwitchModule(settingModule, true);
                    modules.Remove(settingModule);
                    new System.Threading.Tasks.Task(() =>
                    {
                        while (settingModule.IsRunning)
                        {
                            if (Current != settingModule)
                                settingModule.Stop();
                        }
                    }).Start();
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
            Config Config = new Config();
            try
            {
                Assembly dll = Assembly.LoadFile(absolutePath);

                foreach (Type type in dll.GetExportedTypes())
                {
                    Console.WriteLine($"Found Type: {type.Name}");
                    if (type.BaseType == typeof(PixelModule))
                    {
                        PixelModule tmp = (PixelModule)Activator.CreateInstance(type);
                        Logger.Log(ConsoleColor.Blue, $"[{type.Name}]:", ConsoleColor.White, "Loading module");
                        var cfg = Config.GetModule(type.Name);
                        if (cfg == null)
                        {
                            Logger.Log(ConsoleColor.Blue, $"[{type.Name}]", ConsoleColor.Cyan, " Config not found, creating new");
                            Config.CreateModule(tmp);
                            cfg = Config.GetModule(type.Name);
                        }
                        else
                        {
                            //Konfiguracja początkowa modułów
                            foreach (var param in cfg.Params)
                            {
                                var prop = type.GetProperty(param.Key);
                                if (prop.PropertyType == typeof(int))
                                    prop.SetValue(tmp, int.Parse(param.Value));
                                else if (prop.PropertyType == typeof(bool))
                                    prop.SetValue(tmp, bool.Parse(param.Value));
                                else if (prop.PropertyType == typeof(string))
                                    prop.SetValue(tmp, param.Value);
                                else if (prop.PropertyType == typeof(Color))
                                    prop.SetValue(tmp, ColorTranslator.FromHtml(param.Value));
                                else if (prop.PropertyType.BaseType == typeof(Enum))
                                    prop.SetValue(tmp, Enum.Parse(prop.PropertyType, param.Value));
                                else if (prop.PropertyType == typeof(TimeSpan))
                                {
                                    var time = Array.ConvertAll(param.Value.Split(':'), int.Parse);
                                    prop.SetValue(tmp, new TimeSpan(time[0], time[1], time[2]));
                                }
                                else
                                    Logger.Log(ConsoleColor.Blue, $"[{tmp.Name}]:", ConsoleColor.Red, "Undefined Type!");
                                Logger.Log(ConsoleColor.DarkMagenta, "Setting: ", ConsoleColor.White, $"{prop.Name} = {prop.GetValue(tmp)}, Type: {prop.PropertyType.Name}");
                            }
                        }

                        if (cfg.Start)
                        {
                            tmp.Start(Program.UpTime);
                        }
                        modules.Add(tmp);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, e.Message);
            }
        }
        public void Start()
        {
            //Wczytywanie modułów
            modules.Clear();

            var modulesFiles = Directory.GetFiles("Modules/");
            foreach (var moduleFile in modulesFiles)
            {
                var absolutePath = AppDomain.CurrentDomain.BaseDirectory + moduleFile;
                Logger.Log(ConsoleColor.Green, $"Loading: {moduleFile}");
                LoadModule(absolutePath);
            }
            //var sql = new SQL();
            //foreach (var module in Config.Modules)
            //{
            //    LoadModule(sql.GetModuleFromDB(module.Class));
            //}
            IsReady = true;

            Screen.Brightness = new Config().Brightness;
            Logger.Log(ConsoleColor.Cyan, "System Ready!");
            drawThread = new Thread(Render);
            LoadingAnimation.Stop();
            drawThread.Start();
        }
        public void Stop()
        {
            Logger.Log("Stopping Renderer");
            stop = true;
            while (IsRunning) ;
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
            while (IsRunning) ;
            drawThread = new Thread(Render);
            drawThread.Start();
        }
        string[] moduleOrder;
        void Render()
        {
            var config = new Config();
            stop = false;
            IsRunning = true;

            moduleOrder = config.ModuleOrder;
            modules = modules.OrderBy(m => Array.IndexOf(moduleOrder, m.Name)).ToList();
            while (!stop)
            {
                if (moduleOrder.Length == 0)
                {
                    modules.Add(new NullModule());
                }

                List<PixelModule> list = modules.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    if (stop)
                        break;

                    Current = list[i];
                    
                    if (nextModule || !Current.IsRunning || !Current.Visible)
                    {
                        //Logger.Log("Zmiana modulu!!!");
                        //Logger.Log($"Name: {module.Name}, ON: {module.IsRunning}, Visible: {module.Visible}");
                        nextModule = false;
                        continue;
                    }
                    //TODO Night mode
                    //Logger.Log(ConsoleColor.Cyan, $"Current module: {Current.Name}");
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
                        Current.Draw(timer);
                        Screen.Draw();
                        int end = (int)timer.ElapsedMilliseconds;
                        int elapsed = 33 - (end - start);
                        int delay = elapsed > 0 ? elapsed : 0;
                        Thread.Sleep(delay);
                    }
                    if (!stop)
                    {
                        int offset = -9;
                        timer.Restart();
                        new Task(async () =>
                        {
                            while (offset <= 0)
                            {
                                offset++;
                                await Task.Delay(100);
                            }
                        }).Start();
                        while (offset < 0)
                        {
                            int nextModuleNr = i + 1;
                            while (!list[nextModuleNr].IsRunning || !list[nextModuleNr].Visible)
                            {
                                nextModuleNr = nextModuleNr >= list.Count - 1 ? 0 : nextModuleNr + 1;
                            }

                            int start = (int)timer.ElapsedMilliseconds;

                            Screen.Clear();
                            Current.Draw(timer);
                            Screen.Draw(offset + 10);

                            Screen.Clear();
                            list[nextModuleNr].Draw(timer);
                            Screen.Draw(offset);

                            int end = (int)timer.ElapsedMilliseconds;
                            int elapsed = 33 - (end - start);
                            int delay = elapsed > 0 ? elapsed : 0;
                            Thread.Sleep(delay);
                        }
                    }
                }
            }
            IsRunning = false;
        }
        public void NextModule()
        {
            nextModule = true;
            while (nextModule) ;
        }
        public bool SwitchModule(PixelModule module, bool forcePause = false)
        {
            if (module.IsRunning)
            {
                while (Current != module)
                {
                    NextModule();
                }
                if (forcePause)
                    Pause = true;
                return true;
            }
            else
                return false;
        }
        public PixelModule GetModule(string name)
        {
            return modules.Find(x => x.Name == name);
        }
        public void UpdateConfig()
        {
            new Config().EditModules(modules.ToArray());
        }
    }
}
