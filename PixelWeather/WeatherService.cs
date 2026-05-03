using System;
using System.Net;
using System.Threading.Tasks;
using System.Timers;

namespace PixelWeather
{
    static class WeatherService
    {
        public static readonly Weather Weather = new Weather();
        public static readonly AirData AirData = new AirData();

        static readonly Timer _timer = new Timer(10 * 60 * 1000) { AutoReset = true };

        static WeatherService()
        {
            _timer.Elapsed += (s, e) => Task.Run((Action)Fetch);
            _timer.Start();
            Task.Run((Action)Fetch);
        }

        static void Fetch()
        {
            var cfg = WeatherConfig.Instance;
            Weather.Fetch(cfg.BuildUrl());
            if (cfg.AqiStationId > 0)
                using (var wc = new WebClient())
                    AirData.Fetch(cfg.AqiStationId, wc);
        }
    }
}
