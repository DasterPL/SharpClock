using System;
using System.Threading.Tasks;
using System.Timers;
using SharpClock;

namespace PixelSensor
{
    static class SensorService
    {
        public static float RawTemperature { get; private set; } = 0;
        public static float RawHumidity    { get; private set; } = 0;
        public static float RawPressure    { get; private set; } = 0;
        public static bool  HasError       { get; private set; } = false;

        static BME280 _sensor;
        static readonly Timer _timer = new Timer(10_000) { AutoReset = true };

        static SensorService()
        {
            try { _sensor = new BME280(); }
            catch (Exception e) { Logger.Log(ConsoleColor.Red, $"[SensorService]: {e.Message}"); }

            _timer.Elapsed += (s, e) => Task.Run((Action)Read);
            _timer.Start();
            Task.Run((Action)Read);
        }

        static void Read()
        {
            if (_sensor == null) { HasError = true; return; }
            try
            {
                RawTemperature = _sensor.ReadTemperature();
                RawHumidity    = _sensor.ReadHumidity();
                RawPressure    = _sensor.ReadPreasure() / 100f;
                HasError       = false;
            }
            catch (Exception e)
            {
                HasError = true;
                Logger.Log(ConsoleColor.Blue, "[SensorService]:", ConsoleColor.Red, e.Message);
            }
        }
    }
}
