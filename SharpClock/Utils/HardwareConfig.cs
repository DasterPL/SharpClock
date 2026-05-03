using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpClock
{
    static class HardwareConfig
    {
        static readonly Dictionary<string, string> _values;

        static HardwareConfig()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hardware.env");
            _values = File.Exists(path)
                ? File.ReadAllLines(path)
                    .Where(l => !l.TrimStart().StartsWith("#") && l.Contains("="))
                    .ToDictionary(
                        l => l.Substring(0, l.IndexOf('=')).Trim(),
                        l => l.Substring(l.IndexOf('=') + 1).Trim())
                : new Dictionary<string, string>();
        }

        static int Get(string key, int defaultValue) =>
            _values.TryGetValue(key, out string v) && int.TryParse(v, out int r) ? r : defaultValue;

        // LED strip
        public static int LedPin   => Get("LED_PIN",   18);
        public static int LedDma   => Get("LED_DMA",   10);
        public static int LedFreq  => Get("LED_FREQ",  800000);

        // Buttons (BCM numbers)
        public static int BtnPause => Get("BTN_PAUSE", 17);
        public static int BtnNext  => Get("BTN_NEXT",  27);
        public static int BtnUser1 => Get("BTN_USER1", 22);
        public static int BtnUser2 => Get("BTN_USER2", 23);
        public static int BtnUser3 => Get("BTN_USER3", 24);

        // Buzzer
        public static int BuzzerPin => Get("BUZZER_PIN", 25);

        // Render loop
        public static int FrameMs   => Math.Max(1000 / Math.Max(Get("FPS", 30), 1), 1);
    }
}
