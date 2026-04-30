using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml;
using SharpClock;

namespace PixelWeather
{
    class Weather
    {
        public enum WeatherType { no, Thunderstorm, Drizzle, Rain, Snow, Mist, Clear, Clouds }

        string _temperature;
        WeatherType _type;
        int _timezone;

        public string Humidity  { get; set; }
        public string Pressure  { get; set; }
        public string WindSpeed { get; private set; }
        public string WindDir   { get; private set; }
        public DateTime SunRise { get; private set; }
        public DateTime SunSet  { get; private set; }
        public bool HasError    { get; private set; }

        public string Temperature
        {
            get => _temperature;
            set
            {
                try { _temperature = Math.Round(float.Parse(value), 1).ToString("0.0"); }
                catch { _temperature = "0.0"; }
            }
        }

        readonly System.Collections.Generic.Dictionary<string, Image>    _images    = new System.Collections.Generic.Dictionary<string, Image>();
        readonly System.Collections.Generic.Dictionary<string, GifImage> _gifImages = new System.Collections.Generic.Dictionary<string, GifImage>();

        public Weather()
        {
            SetNull();
            var asm = Assembly.GetExecutingAssembly();
            _images["null"]     = Image.FromFile("img/null.png");
            _images["Humidity"] = Image.FromStream(asm.GetManifestResourceStream("PixelWeather.Weather.Humidity.png"));

            foreach (var name in new[] { "Thunderstorm", "Drizzle", "Rain", "Snow", "Mist", "Clear", "Clear_night", "fewClouds", "Clouds", "Wind" })
                _gifImages[name] = new GifImage(Image.FromStream(asm.GetManifestResourceStream($"PixelWeather.Weather.{name}.gif")));
        }

        public void SetNull()
        {
            _temperature = "0.0";
            Humidity  = "0";
            Pressure  = "0";
            _type     = WeatherType.no;
            WindSpeed = "0.0";
            WindDir   = "";
            SunRise   = new DateTime(1970, 1, 1, 6, 0, 0);
            SunSet    = new DateTime(1970, 1, 1, 20, 0, 0);
            _timezone = 0;
        }

        public void Fetch(string url)
        {
            using (var wc = new WebClient())
            {
                try
                {
                    var xml = new XmlDocument();
                    xml.LoadXml(wc.DownloadString(url));
                    var root = xml.DocumentElement;
                    SetSun(root["city"]["sun"].GetAttribute("rise"), root["city"]["sun"].GetAttribute("set"), root["city"]["timezone"].InnerText);
                    Temperature = root["temperature"].GetAttribute("value");
                    Humidity    = root["humidity"].GetAttribute("value");
                    Pressure    = root["pressure"].GetAttribute("value");
                    var dirEl  = root["wind"]["direction"];
                    string dir = dirEl?.GetAttribute("code") ?? "";
                    if (string.IsNullOrEmpty(dir) && float.TryParse(dirEl?.GetAttribute("value"),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float deg))
                        dir = DegreesToCardinal(deg);
                    SetWind(root["wind"]["speed"].GetAttribute("value"), dir);
                    SetWeatherType(root["weather"].GetAttribute("number"));
                    HasError = false;
                }
                catch (Exception e)
                {
                    HasError = true;
                    Logger.Log(ConsoleColor.Blue, "[Weather]:", ConsoleColor.Red, e.Message);
                    SetNull();
                }
            }
        }

        void SetWeatherType(string idString)
        {
            if (!int.TryParse(idString, out int id)) { _type = WeatherType.no; return; }
            if      (id >= 200 && id <= 232) _type = WeatherType.Thunderstorm;
            else if (id >= 300 && id <= 321) _type = WeatherType.Drizzle;
            else if (id >= 500 && id <= 531) _type = WeatherType.Rain;
            else if (id >= 600 && id <= 622) _type = WeatherType.Snow;
            else if (id >= 701 && id <= 781) _type = WeatherType.Mist;
            else if (id == 800)              _type = WeatherType.Clear;
            else if (id >= 801 && id <= 804) _type = WeatherType.Clouds;
            else                             _type = WeatherType.no;
        }

        void SetWind(string speed, string dir)
        {
            WindSpeed = (float.Parse(speed, System.Globalization.CultureInfo.InvariantCulture) * 3.6f).ToString("0.0");
            WindDir = dir;
        }

        static string DegreesToCardinal(float deg)
        {
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[(int)((deg / 45f) + 0.5f) % 8];
        }

        void SetSun(string rise, string set, string timezone)
        {
            int.TryParse(timezone, out _timezone);
            SunRise = ParseDateTime(rise).AddSeconds(_timezone);
            SunSet  = ParseDateTime(set).AddSeconds(_timezone);
        }

        static DateTime ParseDateTime(string s)
        {
            var parts = s.Split('T');
            var d = parts[0].Split('-').Select(int.Parse).ToArray();
            var t = parts[1].Split(':').Select(int.Parse).ToArray();
            return new DateTime(d[0], d[1], d[2], t[0], t[1], t[2]);
        }

        Image CurrentIcon()
        {
            var now = DateTime.Now;
            switch (_type)
            {
                case WeatherType.Thunderstorm: return _gifImages["Thunderstorm"].Advance();
                case WeatherType.Drizzle:      return _gifImages["Drizzle"].Advance();
                case WeatherType.Rain:         return _gifImages["Rain"].Advance();
                case WeatherType.Snow:         return _gifImages["Snow"].Advance();
                case WeatherType.Mist:         return _gifImages["Mist"].Advance();
                case WeatherType.Clear:
                    return now > SunRise && now < SunSet ? _gifImages["Clear"].Advance() : _gifImages["Clear_night"].Advance();
                case WeatherType.Clouds:
                    return now > SunRise && now < SunSet ? _gifImages["fewClouds"].Advance() : _gifImages["Clouds"].Advance();
                default: return _images["null"];
            }
        }

        public void DrawTemp(IPixelDraw screen)
        {
            screen.SetImage(CurrentIcon(), 0);
            if (HasError) { screen.SetText("---", Color.DarkRed, 10); return; }
            var temp = _temperature.Split('.');
            int pos0 = temp[0].Length == 1 ? 12 : 10;
            int pos = screen.SetText(temp[0], Color.DarkRed, pos0, 0);
            if (double.Parse(_temperature) > -10)
            {
                int pos2 = DateTime.Now.Second % 2 == 0 ? screen.SetText(".", Color.DarkBlue, pos, 0) : pos + 1;
                screen.SetText(temp[1], Color.DarkRed, pos2);
            }
            screen.SetText("*C", Color.DarkBlue, 23);
        }

        public void DrawHumidity(IPixelDraw screen)
        {
            screen.SetImage(_images["Humidity"], 0);
            if (HasError) { screen.SetText("---", Color.DarkRed, 14); return; }
            int pos = screen.SetText(Humidity, Color.DarkRed, 14);
            screen.SetText("%", Color.DarkBlue, pos);
        }

        public void DrawWind(IPixelDraw screen)
        {
            screen.SetImage(_gifImages["Wind"].Advance(), 0);
            if (HasError) { screen.SetText("---", Color.DarkRed, 8); return; }
            var ws = WindSpeed.Split('.');
            int pos  = screen.SetText(ws[0], Color.DarkRed, 8, 0);
            int pos1 = DateTime.Now.Second % 2 == 0 ? screen.SetText(".", Color.DarkBlue, pos, 0) : pos + 1;
            screen.SetText(ws[1], Color.DarkRed, pos1);
            screen.SetText(WindDir, Color.DarkBlue, 22);
        }

        public void DrawPressure(IPixelDraw screen)
        {
            if (HasError) { screen.SetText("---", Color.DarkRed, 0); return; }
            screen.SetText(Pressure, Color.DarkRed, 0);
            screen.SetText("hPa", Color.DarkBlue, 20, 0);
        }
    }
}
