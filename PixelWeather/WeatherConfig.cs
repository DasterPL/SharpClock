using SharpClock;

namespace PixelWeather
{
    public enum LocationType { name, id, cord, zip }

    public class WeatherConfig : PixelGlobalSettings
    {
        public static readonly WeatherConfig Instance = new WeatherConfig();

        public string       ApiKey       { get; set; } = "";
        public string       Location     { get; set; } = "Warsaw,pl";
        public LocationType LocationBy   { get; set; } = LocationType.name;
        public int          AqiStationId { get; set; } = 0;

        WeatherConfig() : base("PixelWeather")
        {
            Settings
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

        public string BuildUrl()
        {
            string key = ApiKey;
            string loc = Location;
            switch (LocationBy)
            {
                case LocationType.id:
                    return $"https://api.openweathermap.org/data/2.5/weather?id={loc}&units=metric&mode=xml&appid={key}";
                case LocationType.cord:
                    var c = loc.Split(',');
                    return $"https://api.openweathermap.org/data/2.5/weather?lat={c[0]}&lon={c[1]}&units=metric&mode=xml&appid={key}";
                case LocationType.zip:
                    return $"https://api.openweathermap.org/data/2.5/weather?zip={loc}&units=metric&mode=xml&appid={key}";
                default:
                    return $"https://api.openweathermap.org/data/2.5/weather?q={loc}&units=metric&mode=xml&appid={key}";
            }
        }
    }
}
