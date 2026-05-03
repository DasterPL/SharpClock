using SharpClock;

namespace PixelFoxCloud
{
    public class FoxConfig : PixelGlobalSettings
    {
        public static readonly FoxConfig Instance = new FoxConfig();

        public string ApiKey   { get; set; } = "";
        public string DeviceSN { get; set; } = "";

        FoxConfig() : base("PixelFoxCloud")
        {
            Settings
                .Add(nameof(ApiKey), () => ApiKey, v => ApiKey = v)
                    .Label("pl", "Klucz API FoxESS").Label("en", "FoxESS API Key").Password()
                .Add(nameof(DeviceSN), () => DeviceSN, v => DeviceSN = v)
                    .Label("pl", "Nr seryjny inwertera").Label("en", "Inverter SN");
        }
    }
}
