using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Reflection;
using System.Xml;
using SharpClock;

namespace PixelWeather
{
    public class PixelWeather : PixelModule
    {
        [VisibleNameEnum(lang = "pl", values = new string[]{"Temp=Temperatura", "Humidity=Wilgotność", "Wind=Wiatr", "Pressure=Ciśnienie" })]
        [VisibleNameEnum(lang = "en", values = new string[]{"Temp=Temperature", "Humidity=Humidity", "Wind=Wind", "Pressure=Pressure" })]
        public enum SubModule { Temp, Humidity, Wind, Pressure }
        [VisibleNameEnum(lang = "pl", values = new string[] { "name=Nazwa", "id=Id", "cord=Współrzędne", "zip=Kod pocztowy" })]
        [VisibleNameEnum(lang = "en", values = new string[] { "name=Name", "id=Id", "cord=Coordinates", "zip=Zip code" })]
        public enum LocationType { name, id, cord, zip }
        [VisibleName(lang = "pl", value = "Pokaż")]
        [VisibleName(lang = "en", value = "Show")]
        public SubModule ModuleToShow { get; set; } = SubModule.Temp;
        [VisibleName(lang = "pl", value = "Lokalizacja")]
        [VisibleName(lang = "en", value = "Location")]
        public string Location { get; set; } = "Warsaw,pl";
        [VisibleName(lang = "pl", value = "Lokalizuj przez")]
        [VisibleName(lang = "en", value = "Location by")]
        public LocationType LocationBy { get; set; } = LocationType.name;
        //public string debugTemp { get => weather?.Temperature; set { if (weather != null) weather.Temperature = value; } }

        Weather weather;
        string url;
        string API = "a3bd5419255223bdeda02e5b0be2e58f";
        Dictionary<string, Image> WeatherImages = new Dictionary<string, Image>();
        Dictionary<string, GifImage> GifWeatherImages = new Dictionary<string, GifImage>();
        Image GetWeatherImage(Weather.Type type)
        {
            var time = DateTime.Now;
            switch (type)
            {
                case Weather.Type.no:
                    return WeatherImages["null"];
                case Weather.Type.Thunderstorm:
                    return GifWeatherImages["Thunderstorm"].GetCurrentFrame;
                case Weather.Type.Drizzle:
                    return GifWeatherImages["Drizzle"].GetCurrentFrame;
                case Weather.Type.Rain:
                    return GifWeatherImages["Rain"].GetCurrentFrame;
                case Weather.Type.Snow:
                    return GifWeatherImages["Snow"].GetCurrentFrame;
                case Weather.Type.Mist:
                    return GifWeatherImages["Mist"].GetCurrentFrame;
                case Weather.Type.Clear:
                    if(time > weather.SunRise && time < weather.SunSet)
                        return GifWeatherImages["Clear"].GetCurrentFrame;
                    else
                        return GifWeatherImages["ClearNight"].GetCurrentFrame;
                case Weather.Type.Clouds:
                    if (time > weather.SunRise && time < weather.SunSet)
                        return GifWeatherImages["Clouds"].GetCurrentFrame;
                    else
                        return GifWeatherImages["CloudsNight"].GetCurrentFrame;
                default:
                    return WeatherImages["null"];
            }
        }
        public PixelWeather()
        {
            Icon = "cloud";
            var assembly = Assembly.GetExecutingAssembly();
            WeatherImages.Add("null", Image.FromFile("img/null.png"));
            WeatherImages.Add("Humidity", Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Humidity.png")));
            
            GifWeatherImages.Add("Thunderstorm", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Thunderstorm.gif"))));
            GifWeatherImages.Add("Drizzle", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Drizzle.gif"))));
            GifWeatherImages.Add("Rain", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Rain.gif"))));
            GifWeatherImages.Add("Snow", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Snow.gif"))));
            GifWeatherImages.Add("Mist", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Mist.gif"))));
            GifWeatherImages.Add("Clear", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Clear.gif"))));
            GifWeatherImages.Add("ClearNight", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Clear_night.gif"))));
            GifWeatherImages.Add("Clouds", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.fewClouds.gif"))));
            GifWeatherImages.Add("CloudsNight", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Clouds.gif"))));
            GifWeatherImages.Add("Wind", new GifImage(Image.FromStream(assembly.GetManifestResourceStream($"PixelWeather.Weather.Wind.gif"))));

            Tickrate = 1000 * 60 * 10;
        }
        protected override void Update(Stopwatch stopwatch)
        {
            switch (LocationBy )
            {
                case LocationType.name:
                    url = $"https://api.openweathermap.org/data/2.5/weather?q={Location}&units=metric&mode=xml&appid={API}";
                    break;
                case LocationType.id:
                    url = $"https://api.openweathermap.org/data/2.5/weather?id={Location}&units=metric&mode=xml&appid={API}";
                    break;
                case LocationType.cord:
                    var locArr = Location.Split(',');
                    var lat = locArr[0];
                    var lon = locArr[1];
                    url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&units=metric&mode=xml&appid={API}";
                    break;
                case LocationType.zip:
                    url = $"https://api.openweathermap.org/data/2.5/weather?zip={Location}&units=metric&mode=xml&appid={API}";
                    break;
            }
            using (WebClient wc = new WebClient())
            {
                XmlDocument xml = new XmlDocument();
                try
                {
                    weather = new Weather();
                    //Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Loading weather data");
                    xml.LoadXml(wc.DownloadString(url));
                    //Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Loading weather Completed!");
                    weather.SetSun(xml.DocumentElement["city"]["sun"].GetAttribute("rise"), xml.DocumentElement["city"]["sun"].GetAttribute("set"), xml.DocumentElement["city"]["timezone"].InnerText);
                    weather.Temperature = xml.DocumentElement["temperature"].GetAttribute("value");
                    weather.Humidity = xml.DocumentElement["humidity"].GetAttribute("value");
                    weather.Pressure = xml.DocumentElement["pressure"].GetAttribute("value");
                    weather.SetWind(xml.DocumentElement["wind"]["speed"].GetAttribute("value"), xml.DocumentElement["wind"]["direction"].GetAttribute("code"));
                    weather.SetWeather(xml.DocumentElement["weather"].GetAttribute("number"));
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:",ConsoleColor.Red,"[Error] ", ConsoleColor.White, e.Message);
                    weather.SetNull();
                }
            }
        }
        void DrawTemp()
        {
            Screen.SetImage(GetWeatherImage(weather.GetWeather()), 0);
            var temp = weather.Temperature.Split('.');
            int pos0 = 10;
            if (temp[0].Length == 1)
                pos0 = 12;
            int pos = Screen.SetText(temp[0], Color.DarkRed, pos0, 0);
            if (double.Parse(weather.Temperature) > -10)
            {
                int pos2 = DateTime.Now.Second % 2 == 0 ? Screen.SetText(".", Color.DarkBlue, pos, 0) : pos + 1;
                Screen.SetText(temp[1], Color.DarkRed, pos2);
            }
            Screen.SetText("*C", Color.DarkBlue, 23);       
        }
        void DrawHumidity()
        {
            Screen.SetImage(WeatherImages["Humidity"], 0);
            int pos = Screen.SetText(weather.Humidity, Color.DarkRed, 14);
            Screen.SetText("%", Color.DarkBlue, pos);
        }
        void DrawWind()
        {
            Screen.SetImage(GifWeatherImages["Wind"].GetCurrentFrame, 0);

            var windSpeed = weather.WindSpeed.Split('.');
            int pos = Screen.SetText(windSpeed[0], Color.DarkRed, 8, 0);
            int pos1 = DateTime.Now.Second % 2 == 0 ? Screen.SetText(".", Color.DarkBlue, pos, 0) : pos + 1;
            Screen.SetText(windSpeed[1], Color.DarkRed, pos1);
            //Screen.SetText("km/h", Color.DarkBlue, 20, 0);
            Screen.SetText(weather.WindDir, Color.DarkBlue, 22);
        }
        void DrawPressure()
        {
            Screen.SetText(weather.Pressure, Color.DarkRed, 0);
            Screen.SetText("hPa", Color.DarkBlue, 20, 0);
        }
        public override void Draw(Stopwatch stopwatch = null)
        {
            switch (ModuleToShow)
            {
                case SubModule.Temp:
                    DrawTemp();
                    break;
                case SubModule.Humidity:
                    DrawHumidity();
                    break;
                case SubModule.Wind:
                    DrawWind();
                    break;
                case SubModule.Pressure:
                    DrawPressure();
                    break;
            }
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            switch (ModuleToShow)
            {
                case SubModule.Temp:
                    ModuleToShow = SubModule.Humidity;
                    break;
                case SubModule.Humidity:
                    ModuleToShow = SubModule.Wind;
                    break;
                case SubModule.Wind:
                    ModuleToShow = SubModule.Pressure;
                    break;
                case SubModule.Pressure:
                    ModuleToShow = SubModule.Temp;
                    break;
            }
            pixelRenderer.UpdateConfig();
        }

    }
}
