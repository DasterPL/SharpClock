using Newtonsoft.Json.Linq;
using SharpClock;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PixelFoxCloud
{
    public class PixelFoxCloud : PixelModule
    {
        public enum SubModule { PvPower, Today }
        public enum DisplayMode { Static, Rotating }

        public string ApiKey { get; set; } = "";
        public string DeviceSN { get; set; } = "";
        public DisplayMode Mode { get; set; } = DisplayMode.Rotating;
        public int RotationInterval { get; set; } = 5000;
        public SubModule ModuleToShow { get; set; } = SubModule.PvPower;

        float pvPower = 0;
        float todayYield = 0;
        bool hasData = false;
        int _zeroPowerCount = 0;
        const int ZeroHideThreshold = 3;

        const int TransitionMs = 266;
        long _prevStopwatchMs = -1;
        long _accSubModuleMs = 0;

        static readonly GifImage _logo = new GifImage(Image.FromStream(
            Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelFoxCloud.assets.logo.gif")));
        static readonly Image _storedIcon = Image.FromStream(
            Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelFoxCloud.assets.Stored.png"));

        public PixelFoxCloud()
        {
            Icon = "bolt";
            Tickrate = 1000 * 60 * 5;

            Settings
                .Add(nameof(ApiKey), () => ApiKey, v => ApiKey = v)
                    .Label("pl", "Klucz API").Label("en", "API Key").Password()
                .Add(nameof(DeviceSN), () => DeviceSN, v => DeviceSN = v)
                    .Label("pl", "Nr seryjny inwertera").Label("en", "Inverter SN")
                .Add(nameof(Mode), () => Mode, v => Mode = v)
                    .Label("pl", "Tryb").Label("en", "Mode")
                    .EnumLabel("pl", "Static=Stały", "Rotating=Zmienny")
                    .EnumLabel("en", "Static=Static", "Rotating=Rotating")
                .Add(nameof(RotationInterval), () => RotationInterval, v => RotationInterval = v)
                    .Label("pl", "Czas zmiany (ms)").Label("en", "Rotation interval (ms)")
                    .Range(100, 30000)
                    .When(() => Mode == DisplayMode.Rotating)
                .Add(nameof(ModuleToShow), () => ModuleToShow, v => ModuleToShow = v)
                    .Label("pl", "Pokaż").Label("en", "Show")
                    .EnumLabel("pl", "PvPower=Moc PV", "Today=Dziś")
                    .EnumLabel("en", "PvPower=PV Power", "Today=Today")
                    .When(() => Mode == DisplayMode.Static);
        }

        void DrawPvPower()
        {
            Screen.SetImage(_logo.Advance(), 0);
            if (!hasData) { Screen.SetText("---", Color.DarkRed, 9); return; }
            string val = pvPower >= 1f
                ? pvPower.ToString("#.##") + "k"
                : ((int)(pvPower * 1000)).ToString() + "W";
            Screen.SetText(val, Color.DarkGreen, 9, 1);
        }

        void DrawToday()
        {
            Screen.SetImage(_storedIcon, 0);
            if (!hasData) { Screen.SetText("---", Color.DarkRed, 9); return; }
            int pos = Screen.SetText(todayYield.ToString("#.#"), Color.DarkGoldenrod, 9, 1);
            Screen.SetText("k", Color.Goldenrod, pos + 1, 0);
        }

        void DrawSubModule(SubModule sub)
        {
            switch (sub)
            {
                case SubModule.PvPower: DrawPvPower(); break;
                case SubModule.Today:   DrawToday();   break;
            }
        }

        public override void Draw(Stopwatch stopwatch)
        {
            if (Mode == DisplayMode.Static || stopwatch == null)
            {
                DrawSubModule(ModuleToShow);
                return;
            }

            long cur = stopwatch.ElapsedMilliseconds;
            if (_prevStopwatchMs >= 0 && cur >= _prevStopwatchMs)
                _accSubModuleMs += cur - _prevStopwatchMs;
            _prevStopwatchMs = cur;

            var values = (SubModule[])Enum.GetValues(typeof(SubModule));
            int currentIdx = (int)(_accSubModuleMs / RotationInterval % values.Length);
            long timeUntilNext = RotationInterval - _accSubModuleMs % RotationInterval;

            if (timeUntilNext > TransitionMs)
            {
                DrawSubModule(values[currentIdx]);
                return;
            }

            int nextIdx = (currentIdx + 1) % values.Length;
            int xOff = Math.Min((int)((TransitionMs - timeUntilNext) * 32 / TransitionMs) + 1, 32);

            DrawSubModule(values[currentIdx]);
            var currentBuffer = Screen.GetBuffer();
            Screen.Clear();
            DrawSubModule(values[nextIdx]);
            var nextBuffer = Screen.GetBuffer();
            Screen.DrawFromBuffersX(currentBuffer, nextBuffer, xOff);
        }

        void NextSubModule()
        {
            var values = (SubModule[])Enum.GetValues(typeof(SubModule));
            if (Mode == DisplayMode.Static)
                ModuleToShow = values[((int)ModuleToShow + 1) % values.Length];
            else
                _accSubModuleMs = (_accSubModuleMs / RotationInterval + 1) * RotationInterval;
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            switch (button)
            {
                case ButtonId.User1: NextSubModule(); break;
                case ButtonId.User2:
                    Mode = Mode == DisplayMode.Static ? DisplayMode.Rotating : DisplayMode.Static;
                    break;
            }
            pixelRenderer.UpdateConfig();
        }

        protected override void Update(Stopwatch stopwatch)
        {
            int hour = DateTime.Now.Hour;
            if (hour < 5 || hour >= 21)
            {
                _zeroPowerCount = 0;
                Visible = false;
                return;
            }

            if (string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(DeviceSN))
            {
                Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Yellow, "ApiKey or DeviceSN not configured.");
                return;
            }

            try
            {
                FetchRealTime();
                FetchTodayYield();
            }
            catch (Exception e)
            {
                hasData = false;
                Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Red, e.Message);
                return;
            }

            if (pvPower > 0.01f)
            {
                _zeroPowerCount = 0;
                Visible = true;
            }
            else if (++_zeroPowerCount >= ZeroHideThreshold)
            {
                Visible = false;
            }
        }

        void FetchRealTime()
        {
            const string path = "/op/v0/device/real/query";
            var body = new JObject
            {
                ["sn"] = DeviceSN,
                ["variables"] = new JArray("pvPower")
            };

            var result = Post(path, body);
            if (result == null) return;

            var dict = new Dictionary<string, float>();
            var datas = result[0]?["datas"] as JArray ?? result;
            foreach (var item in datas)
                dict[(string)item["variable"]] = item["value"]?.Value<float>() ?? 0f;

            dict.TryGetValue("pvPower", out pvPower);
            hasData = true;
        }

        void FetchTodayYield()
        {
            const string path = "/op/v0/device/report/query";
            var now = DateTime.Now;
            var body = new JObject
            {
                ["sn"] = DeviceSN,
                ["dimension"] = "month",
                ["year"] = now.Year,
                ["month"] = now.Month,
                ["variables"] = new JArray("generation")
            };

            var result = Post(path, body);
            if (result == null) return;

            var values = result[0]?["values"] as JArray;
            int dayIdx = now.Day - 1;
            if (values != null && dayIdx < values.Count)
                todayYield = values[dayIdx]?.Value<float>() ?? 0f;
        }

        JArray Post(string path, JObject body)
        {
            string url = "https://www.foxesscloud.com" + path;
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = Md5(path + @"\r\n" + ApiKey + @"\r\n" + timestamp);

            using (var wc = new WebClient())
            {
                wc.Headers["token"] = ApiKey;
                wc.Headers["timestamp"] = timestamp;
                wc.Headers["signature"] = signature;
                wc.Headers["lang"] = "en";
                wc.Headers["Content-Type"] = "application/json";

                string response = wc.UploadString(url, "POST", body.ToString(Newtonsoft.Json.Formatting.None));
                Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Gray, $"{path}: {response.Substring(0, Math.Min(200, response.Length))}");
                var json = JObject.Parse(response);

                int errno = json["errno"]?.Value<int>() ?? -1;
                if (errno != 0)
                {
                    string msg = json["msg"]?.ToString() ?? json["message"]?.ToString() ?? "no message";
                    Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Red, $"API error {errno}: {msg} [{path}]");
                    return null;
                }

                return json["result"] as JArray;
            }
        }

        static string Md5(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(32);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
