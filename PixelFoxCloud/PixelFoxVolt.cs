using SharpClock;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

namespace PixelFoxCloud
{
    public class FoxPvPower : PixelModule
    {
        static readonly GifImage _logo = new GifImage(Image.FromStream(
            Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelFoxCloud.assets.logo.gif")));

        public FoxPvPower() { Icon = "bolt"; Tickrate = 30_000; }

        protected override void Update(Stopwatch sw) { Visible = FoxService.ShouldShow; }

        public override void Draw(Stopwatch sw)
        {
            Screen.SetImage(_logo.Advance(), 0);
            if (!FoxService.HasData) { Screen.SetText("---", Color.DarkRed, 9); return; }
            float p = FoxService.PvPower;
            string val = p >= 1f ? p.ToString("#.##") + "k" : ((int)(p * 1000)).ToString() + "W";
            Screen.SetText(val, Color.DarkGreen, 9, 1);
        }
    }

    public class FoxToday : PixelModule
    {
        static readonly Image _storedIcon = Image.FromStream(
            Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelFoxCloud.assets.Stored.png"));

        public FoxToday() { Icon = "wb_sunny"; Tickrate = 30_000; }

        protected override void Update(Stopwatch sw) { Visible = FoxService.ShouldShow; }

        public override void Draw(Stopwatch sw)
        {
            Screen.SetImage(_storedIcon, 0);
            if (!FoxService.HasData) { Screen.SetText("---", Color.DarkRed, 9); return; }
            int pos = Screen.SetText(FoxService.TodayYield.ToString("#.#"), Color.DarkGoldenrod, 9, 1);
            Screen.SetText("k", Color.Goldenrod, pos + 1, 0);
        }
    }
}
