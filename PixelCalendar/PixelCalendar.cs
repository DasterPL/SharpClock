using System;
using System.Diagnostics;
using System.Drawing;
using SharpClock;

namespace PixelCalendar
{
    public class PixelCalendar : PixelModule
    {
        DateTime Date;
        string[] Week = { "Nd", "Pn", "Wt", "Sr", "Cz", "Pt", "So" };
        public PixelCalendar()
        {
            Icon = "date_range";
        }
        protected override void Update(Stopwatch stopwatch)
        {
            Date = DateTime.Now;
        }
        public override void Draw(Stopwatch stopwatch = null)
        {
            Screen.SetText(Week[(int)Date.DayOfWeek], Color.DarkGreen, 0);
            Screen.SetText(Date.Day.ToString("D2"), Color.DarkRed, 10);
            if (Date.Second % 2 == 0) Screen.SetText(".", Color.DarkBlue, 20);
            Screen.SetTextRoman(Date.Month, Color.DarkOrange, 21);
        }
    }
}
