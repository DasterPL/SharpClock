using System.Diagnostics;
using System.Drawing;
using SharpClock;

namespace PixelText
{
    public class PixelText : PixelModule
    {
        [VisibleName(lang = "pl", value = "Tekst")]
        [VisibleName(lang = "en", value = "Text")]
        public string Text { get; set; } = "Sharp Clock";
        [VisibleName(lang = "pl", value = "Wstrzymaj")]
        [VisibleName(lang = "en", value = "Pause")]
        public bool Pause { get; set; } = false;
        [VisibleName(lang = "pl", value = "Kolor tekstu")]
        [VisibleName(lang = "en", value = "Text Color")]
        public Color Color { get; set; } = Color.White;
        [VisibleName(lang = "pl", value = "Szybkość Przewijania")]
        [VisibleName(lang = "en", value = "Scroll speed")]
        public int Speed { get => Tickrate; set => Tickrate = value; }

        int pos = 2;
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
