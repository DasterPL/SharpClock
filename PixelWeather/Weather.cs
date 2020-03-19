using System;
using System.Linq;
using SharpClock;

namespace PixelWeather
{
    class Weather
    {
        public Weather()
        {
            SetNull();
        }
        public enum Type { no, Thunderstorm, Drizzle, Rain, Snow, Mist, Clear, Clouds };

        string temperature;
        Type type;
        public string Humidity;
        public string Pressure;

        public DateTime SunRise { get; private set; }
        public DateTime SunSet { get; private set; }
        int timezone;

        public void SetNull()
        {
            temperature = "0.0";
            Humidity = "0";
            Pressure = "0";
            type = Type.no;
            WindSpeed = "0.0";
            WindDir = "";
            DateTime SunRise = new DateTime(1970, 1, 1, 6, 0, 0, 0);
            DateTime SunSet = new DateTime(1970, 1, 1, 20, 0, 0, 0);
            timezone = 0;
        }
        public string Temperature
        {
            get => temperature;
            set
            {
                try
                {
                    temperature = Math.Round(float.Parse(value),1).ToString("0.0");
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message);
                    temperature = "no";
                }

            }
        }

        public string WindSpeed { get; private set; }
        public string WindDir { get; private set; }

        public void SetWeather(string idString)
        {
            int id;
            if (int.TryParse(idString, out id) == false)
            {
                type = Type.no;
                return;
            }

            if (id >= 200 && id <= 232)
                type = Type.Thunderstorm;
            else if (id >= 300 && id <= 321)
                type = Type.Drizzle;
            else if (id >= 500 && id <= 531)
                type = Type.Rain;
            else if (id >= 600 && id <= 622)
                type = Type.Snow;
            else if (id >= 701 && id <= 781)
                type = Type.Mist;
            else if (id == 800)
                type = Type.Clear;
            else if (id >= 801 && id <= 804)
                type = Type.Clouds;
            else
                type = Type.no;
        }
        public Type GetWeather()
        {
            return type;
        }
        public void SetWind(string speed, string dir)
        {
            WindSpeed = (float.Parse(speed)*3.6).ToString("0.0");
            WindDir = dir;
        }
        public void SetSun(string rise, string set, string timezone)
        {
            int.TryParse(timezone, out this.timezone);
            var riseArr = rise.Split('T');
            var riseDate = riseArr[0].Split('-').Select(Int32.Parse).ToArray();
            var riseTime = riseArr[1].Split(':').Select(Int32.Parse).ToArray();
            SunRise = new DateTime(riseDate[0], riseDate[1], riseDate[2], riseTime[0], riseTime[1], riseTime[2], 0).AddSeconds(this.timezone); ;

            var setArr = set.Split('T');
            var setDate = setArr[0].Split('-').Select(Int32.Parse).ToArray();
            var setTime = setArr[1].Split(':').Select(Int32.Parse).ToArray();
            SunSet = new DateTime(setDate[0], setDate[1], setDate[2], setTime[0], setTime[1], setTime[2], 0).AddSeconds(this.timezone);

            //Logger.Log($"[Weather module]: Sunrise: {SunRise.ToString()}, Sunset: {SunSet}, Timezone: {this.timezone}");
        }
    }
}
