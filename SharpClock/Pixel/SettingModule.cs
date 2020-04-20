using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    class SettingModule : PixelModule, IDisposable
    {
        int currentFunction = 0;
        int pos = 2;
        List<Function> functions = new List<Function>()
        {
            new Function()
            {
                Name = "Wylacz System",
                Action = ()=>{Program.Kill();  Process.Start("/bin/bash", "-c \"shutdown\""); }
            },
            new Function()
            {
                Name = "Zatrzymaj Program",
                Action = ()=>{Program.Kill();  Environment.Exit(0); }
            },
            new Function()
            {
                Name = "Restart",
                Action = ()=>{Program.Kill();  Process.Start("/bin/bash", "-c \"reboot\""); }
            },
            new Function()
            {
                Name = "Tryb Nocny",
                Action = ()=>{Screen.Brightness = 2; pixelRenderer.SwitchModule(pixelRenderer.GetModules[0],true); }
            },
            new Function()
            {
                Name = "Tryb Dzienny",
                Action = ()=>{Screen.Brightness = 10; pixelRenderer.Pause = false; }
            },
            new Function()
            {
                Name = "Animacja Przewijania (beta)",
                Action = ()=>{PixelRenderer.Pixel.AnimatedSwitching = !PixelRenderer.Pixel.AnimatedSwitching; }
            },
        };
        List<Function> CurrentMenu;
        public SettingModule()
        {
            CurrentMenu = functions;
            Tickrate = 160;
        }
        public override void Draw(Stopwatch stopwatch)
        {
            Screen.SetText(CurrentMenu[currentFunction].Name, Color.White, pos);
        }

        protected override void Update(Stopwatch stopwatch)
        {
            int len = Screen.TextLength(CurrentMenu[currentFunction].Name);
            //if(CurrentMenu[currentFunction].Image != null)
            //{
            //    //TO DO
            //}
            if (len <= 30)
            {
                pos = 1;
            }
            else
            {
                if (pos == 2)
                    Tickrate = 1500;
                else
                    Tickrate = 160;
                if (pos-- <= -len)
                    pos = 31;
            }
           
        }
        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            switch (button)
            {
                case ButtonId.User1:
                    pos = 2;
                    if (--currentFunction < 0)
                        currentFunction = functions.Count - 1;
                    break;
                case ButtonId.User2:
                    pos = 2;
                    if (++currentFunction >= CurrentMenu.Count)
                        currentFunction = 0;
                    break;
                case ButtonId.User3:
                    CurrentMenu[currentFunction].Action();
                    break;
            }
        }
        public void Dispose()
        {
            Stop();
        }
        private class Function
        {
            public string Name { get; set; }
            public Image Image { get; set; } = null;
            public Action Action { get; set; }
        }
    }
}
