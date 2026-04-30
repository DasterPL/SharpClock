using System;
using System.Drawing;
using System.Net;
using Newtonsoft.Json.Linq;
using SharpClock;

namespace PixelWeather
{
    class AirData
    {
        public float Pm25     { get; private set; } = -1;
        public float Pm10     { get; private set; } = -1;
        public bool  HasError { get; private set; } = false;

        const string BaseUrl = "https://api.gios.gov.pl/pjp-api/v1/rest";

        public void Fetch(int stationId, WebClient wc)
        {
            try
            {
                var root    = JObject.Parse(wc.DownloadString($"{BaseUrl}/station/sensors/{stationId}"));
                var sensors = root["Lista stanowisk pomiarowych dla podanej stacji"] as JArray;
                int pm25Id = 0, pm10Id = 0;
                foreach (JObject s in sensors)
                {
                    string code = s["Wskaźnik - kod"]?.ToString() ?? "";
                    int?   sid  = s["Identyfikator stanowiska"]?.Value<int?>();
                    if (sid == null) continue;
                    if (code == "PM2.5") pm25Id = sid.Value;
                    else if (code == "PM10") pm10Id = sid.Value;
                }
                if (pm25Id > 0) Pm25 = ReadSensor(wc, pm25Id);
                if (pm10Id > 0) Pm10 = ReadSensor(wc, pm10Id);
                HasError = false;
            }
            catch (Exception e)
            {
                HasError = true;
                Logger.Log(ConsoleColor.Blue, "[AirData]:", ConsoleColor.Red, e.Message);
            }
        }

        static float ReadSensor(WebClient wc, int sensorId)
        {
            var root   = JObject.Parse(wc.DownloadString($"{BaseUrl}/data/getData/{sensorId}"));
            var values = root["Lista danych pomiarowych"] as JArray;
            foreach (JObject entry in values)
            {
                float? v = entry["Wartość"]?.Value<float?>();
                if (v.HasValue) return v.Value;
            }
            return -1;
        }

        static readonly GifImage _mistIcon = new GifImage(Image.FromStream(
            System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PixelWeather.Weather.Mist.gif")));

        public void Draw(IPixelDraw screen, int stationId)
        {
            if (stationId == 0) { screen.SetTextCentered("AQI?", Color.Gray, 0); return; }
            if (HasError)       { screen.SetTextCentered("AQI!", Color.Red,  0); return; }
            if (Pm25 < 0 && Pm10 < 0) { screen.SetTextCentered("---", Color.Gray, 0); return; }

            screen.SetImage(_mistIcon.Advance(), 12);
            if (Pm25 >= 0)
                screen.SetText(((int)Math.Round(Pm25)).ToString(), QualityColor(Pm25, 12f, 35f), 0, 0);
            if (Pm10 >= 0)
                screen.SetTextRight(((int)Math.Round(Pm10)).ToString(), QualityColor(Pm10, 20f, 50f), 32, 0);
        }

        static Color QualityColor(float val, float good, float moderate)
        {
            if (val <= good)     return Color.Lime;
            if (val <= moderate) return Color.Yellow;
            return Color.Red;
        }
    }
}
