using System.Diagnostics;
using SharpClock;

namespace PixelWeather
{
    public class WeatherTemp : PixelModule
    {
        public WeatherTemp() { Icon = "thermostat"; Tickrate = int.MaxValue; }
        protected override void Update(Stopwatch sw) { }
        public override void Draw(Stopwatch sw) => WeatherService.Instance.Weather.DrawTemp(Screen);
    }

    public class WeatherHumidity : PixelModule
    {
        public WeatherHumidity() { Icon = "water_drop"; Tickrate = int.MaxValue; }
        protected override void Update(Stopwatch sw) { }
        public override void Draw(Stopwatch sw) => WeatherService.Instance.Weather.DrawHumidity(Screen);
    }

    public class WeatherWind : PixelModule
    {
        public WeatherWind() { Icon = "air"; Tickrate = int.MaxValue; }
        protected override void Update(Stopwatch sw) { }
        public override void Draw(Stopwatch sw) => WeatherService.Instance.Weather.DrawWind(Screen);
    }

    public class WeatherPressure : PixelModule
    {
        public WeatherPressure() { Icon = "compress"; Tickrate = int.MaxValue; }
        protected override void Update(Stopwatch sw) { }
        public override void Draw(Stopwatch sw) => WeatherService.Instance.Weather.DrawPressure(Screen);
    }

    public class WeatherAirQuality : PixelModule
    {
        public WeatherAirQuality() { Icon = "foggy"; Tickrate = int.MaxValue; }
        protected override void Update(Stopwatch sw) { }
        public override void Draw(Stopwatch sw)
            => WeatherService.Instance.AirData.Draw(Screen, WeatherConfig.Instance.AqiStationId);
    }
}
