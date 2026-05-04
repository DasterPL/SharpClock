using System;
using SharpClock;

namespace PixelSensor
{
    public class SensorService : PixelService
    {
        public static readonly SensorService Instance = new SensorService();

        public float RawTemperature { get; private set; }
        public float RawHumidity    { get; private set; }
        public float RawPressure    { get; private set; }
        public bool  HasError => !IsRunning || LastError != null;

        readonly BME280 _sensor;
        protected override int IntervalMs => 10_000;

        SensorService()
        {
            try { _sensor = new BME280(); }
            catch (Exception e) { Logger.Log(ConsoleColor.Red, $"[SensorService]: {e.Message}"); }
        }

        protected override void Run()
        {
            if (_sensor == null) throw new InvalidOperationException("Sensor not available");
            RawTemperature = _sensor.ReadTemperature();
            RawHumidity    = _sensor.ReadHumidity();
            RawPressure    = _sensor.ReadPreasure() / 100f;
        }
    }
}
