using System;
using System.Diagnostics;
using System.Net;
using SharpClock;

namespace PixelWeather
{
    public class PixelWeather : PixelModule
    {
        public enum SubModule { Temp, Humidity, Wind, Pressure, AirQuality }
        public enum LocationType { name, id, cord, zip }
        public enum DisplayMode { Static, Rotating }

        public DisplayMode Mode            { get; set; } = DisplayMode.Static;
        public int         RotationInterval{ get; set; } = 5000;
        public SubModule   ModuleToShow    { get; set; } = SubModule.Temp;
        public string      Location        { get; set; } = "Warsaw,pl";
        public LocationType LocationBy     { get; set; } = LocationType.name;
        public string      ApiKey          { get; set; } = "";
        public int         AqiStationId    { get; set; } = 0;

        readonly Weather _weather = new Weather();
        readonly AirData _airData = new AirData();

        public PixelWeather()
        {
            Icon     = "cloud";
            Tickrate = 1000 * 60 * 10;

            Settings
                .Add(nameof(Mode), () => Mode, v => Mode = v)
                    .Label("pl", "Tryb").Label("en", "Mode")
                    .EnumLabel("pl", "Static=Stały", "Rotating=Zmienny")
                    .EnumLabel("en", "Static=Static", "Rotating=Rotating")
                .Add(nameof(RotationInterval), () => RotationInterval, v => RotationInterval = v)
                    .Label("pl", "Czas zmiany (ms)").Label("en", "Rotation interval (ms)")
                    .Range(100, 30000).When(() => Mode == DisplayMode.Rotating)
                .Add(nameof(ModuleToShow), () => ModuleToShow, v => ModuleToShow = v)
                    .Label("pl", "Pokaż").Label("en", "Show")
                    .EnumLabel("pl", "Temp=Temperatura", "Humidity=Wilgotność", "Wind=Wiatr", "Pressure=Ciśnienie", "AirQuality=Jakość powietrza")
                    .EnumLabel("en", "Temp=Temperature", "Humidity=Humidity", "Wind=Wind", "Pressure=Pressure", "AirQuality=Air Quality")
                    .When(() => Mode == DisplayMode.Static)
                .Add(nameof(ApiKey), () => ApiKey, v => ApiKey = v)
                    .Label("pl", "Klucz API OWM").Label("en", "OWM API Key").Password()
                .Add(nameof(LocationBy), () => LocationBy, v => LocationBy = v)
                    .Label("pl", "Lokalizuj przez").Label("en", "Location by")
                    .EnumLabel("pl", "name=Nazwa", "id=Id", "cord=Współrzędne", "zip=Kod pocztowy")
                    .EnumLabel("en", "name=Name", "id=Id", "cord=Coordinates", "zip=Zip code")
                .Add(nameof(Location), () => Location, v => Location = v)
                    .Label("pl", "Lokalizacja").Label("en", "Location")
                .Add(nameof(AqiStationId), () => AqiStationId, v => AqiStationId = v)
                    .Label("pl", "ID stacji GIOŚ").Label("en", "GIOŚ Station ID");
        }

        string BuildUrl()
        {
            switch (LocationBy)
            {
                case LocationType.id:   return $"https://api.openweathermap.org/data/2.5/weather?id={Location}&units=metric&mode=xml&appid={ApiKey}";
                case LocationType.cord:
                    var c = Location.Split(',');
                    return $"https://api.openweathermap.org/data/2.5/weather?lat={c[0]}&lon={c[1]}&units=metric&mode=xml&appid={ApiKey}";
                case LocationType.zip:  return $"https://api.openweathermap.org/data/2.5/weather?zip={Location}&units=metric&mode=xml&appid={ApiKey}";
                default:                return $"https://api.openweathermap.org/data/2.5/weather?q={Location}&units=metric&mode=xml&appid={ApiKey}";
            }
        }

        protected override void Update(Stopwatch stopwatch)
        {
            _weather.Fetch(BuildUrl());
            if (AqiStationId > 0)
            {
                using (var wc = new WebClient())
                    _airData.Fetch(AqiStationId, wc);
            }
        }

        void DrawSubModule(SubModule sub)
        {
            switch (sub)
            {
                case SubModule.Temp:       _weather.DrawTemp(Screen);      break;
                case SubModule.Humidity:   _weather.DrawHumidity(Screen);  break;
                case SubModule.Wind:       _weather.DrawWind(Screen);      break;
                case SubModule.Pressure:   _weather.DrawPressure(Screen);  break;
                case SubModule.AirQuality: _airData.Draw(Screen, AqiStationId); break;
            }
        }

        const int TransitionMs = 266;
        long _prevMs = -1, _accMs = 0;

        public override void Draw(Stopwatch stopwatch = null)
        {
            if (Mode == DisplayMode.Static || stopwatch == null)
            {
                DrawSubModule(ModuleToShow);
                return;
            }

            long cur = stopwatch.ElapsedMilliseconds;
            if (_prevMs >= 0 && cur >= _prevMs) _accMs += cur - _prevMs;
            _prevMs = cur;

            var values     = (SubModule[])Enum.GetValues(typeof(SubModule));
            int curIdx     = (int)(_accMs / RotationInterval % values.Length);
            long untilNext = RotationInterval - _accMs % RotationInterval;

            if (untilNext > TransitionMs)
            {
                DrawSubModule(values[curIdx]);
                return;
            }

            int nextIdx = (curIdx + 1) % values.Length;
            int xOff    = Math.Min((int)((TransitionMs - untilNext) * 32 / TransitionMs) + 1, 32);

            DrawSubModule(values[curIdx]);
            var curBuf = Screen.GetBuffer();
            Screen.Clear();
            DrawSubModule(values[nextIdx]);
            var nextBuf = Screen.GetBuffer();
            Screen.DrawFromBuffersX(curBuf, nextBuf, xOff);
        }

        void NextSubModule()
        {
            var values = (SubModule[])Enum.GetValues(typeof(SubModule));
            if (Mode == DisplayMode.Static)
                ModuleToShow = values[((int)ModuleToShow + 1) % values.Length];
            else
                _accMs = (_accMs / RotationInterval + 1) * RotationInterval;
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
    }
}
