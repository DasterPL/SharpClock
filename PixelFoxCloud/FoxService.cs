using System;
using SharpClock;

namespace PixelFoxCloud
{
    public class FoxService : PixelService
    {
        public static readonly FoxService Instance = new FoxService();

        public float PvPower    { get; private set; }
        public float TodayYield { get; private set; }
        public bool  HasData    => _hasData    && IsRunning;
        public bool  ShouldShow => _shouldShow && IsRunning;

        bool _hasData;
        bool _shouldShow;

        const int ZeroHideThreshold = 3;
        int _zeroPowerCount;

        protected override int IntervalMs => 5 * 60 * 1000;

        FoxService() { }

        protected override void Run()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 5 || hour >= 21)
            {
                _zeroPowerCount = 0;
                _shouldShow = false;
                return;
            }

            string apiKey   = FoxConfig.Instance.ApiKey;
            string deviceSN = FoxConfig.Instance.DeviceSN;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deviceSN))
                return;

            var data = new FoxEssClient(apiKey, deviceSN).Fetch();
            PvPower    = data.PvPower;
            TodayYield = data.TodayYield;
            _hasData   = true;

            if (PvPower > 0.01f) { _zeroPowerCount = 0; _shouldShow = true; }
            else if (++_zeroPowerCount >= ZeroHideThreshold) _shouldShow = false;
        }
    }
}
