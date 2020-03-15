using System.Diagnostics;
using System.Drawing;
using SharpClock;

namespace PixelNativeModules
{
    public class PixelText : PixelModule
    {
        public string Text { get; set; } = "Sharp Clock";
        public bool Pause { get; set; } = false;
        int pos = 2;

        public Color Color { get; set; } = Color.White;
        public int Speed { get => Tickrate; set => Tickrate = value; }

        public PixelText()
        {
            Icon = "message";
        }
        public override void Draw(Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < Tickrate)
                pos = 1;
            Screen.SetText(Text, Color, pos);
        }
        protected override void Update(Stopwatch stopwatch)
        {
            int len = Screen.TextLength(Text);

            if (!Pause)
            {
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
                Timer = (len * Tickrate * 2) + (len < 32 ? 32 - len : 0) * Tickrate;
            }
        }
        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            if (button == ButtonId.User1)
                Pause = !Pause;
            else if (button == ButtonId.User2)
                Speed += 10;
            else if (button == ButtonId.User3)
                Speed -= 10;
            pixelRenderer.UpdateConfig();
        }
    }
}
