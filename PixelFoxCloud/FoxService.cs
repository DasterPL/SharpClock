using System;
using System.Threading.Tasks;
using System.Timers;
using SharpClock;

namespace PixelFoxCloud
{
    static class FoxService
    {
        public static float PvPower    { get; private set; } = 0;
        public static float TodayYield { get; private set; } = 0;
        public static bool  HasData    { get; private set; } = false;
        public static bool  ShouldShow { get; private set; } = false;

        const int ZeroHideThreshold = 3;
        static int _zeroPowerCount = 0;

        static readonly Timer _timer = new Timer(5 * 60 * 1000) { AutoReset = true };

        static FoxService()
        {
            _timer.Elapsed += (s, e) => Task.Run((Action)Fetch);
            _timer.Start();
            Task.Run((Action)Fetch);
        }

        static void Fetch()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 5 || hour >= 21)
            {
                _zeroPowerCount = 0;
                ShouldShow = false;
                return;
            }

            string apiKey  = FoxConfig.Instance.ApiKey;
            string deviceSN = FoxConfig.Instance.DeviceSN;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deviceSN))
            {
                Logger.Log(ConsoleColor.Blue, "[FoxService]:", ConsoleColor.Yellow, "ApiKey or DeviceSN not configured.");
                return;
            }

            try
            {
                var data = new FoxEssClient(apiKey, deviceSN).Fetch();
                PvPower    = data.PvPower;
                TodayYield = data.TodayYield;
                HasData    = true;
            }
            catch (Exception e)
            {
                HasData = false;
                Logger.Log(ConsoleColor.Blue, "[FoxService]:", ConsoleColor.Red, e.Message);
                return;
            }

            if (PvPower > 0.01f)
            {
                _zeroPowerCount = 0;
                ShouldShow = true;
            }
            else if (++_zeroPowerCount >= ZeroHideThreshold)
            {
                ShouldShow = false;
            }
        }
    }
}
