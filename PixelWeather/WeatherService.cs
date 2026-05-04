using System.Net;
using SharpClock;

namespace PixelWeather
{
    public class WeatherService : PixelService
    {
        public static readonly WeatherService Instance = new WeatherService();

        internal readonly Weather Weather = new Weather();
        internal readonly AirData AirData = new AirData();

        protected override int IntervalMs => 10 * 60 * 1000;

        WeatherService() { }

        protected override void Run()
        {
            var cfg = WeatherConfig.Instance;
            Weather.Fetch(cfg.BuildUrl());
            if (cfg.AqiStationId > 0)
                using (var wc = new WebClient())
                    AirData.Fetch(cfg.AqiStationId, wc);
        }
    }
}
