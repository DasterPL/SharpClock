using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using SharpClock;

namespace PixelSensor
{
    public class SensorTemperature : PixelModule
    {
        public float TemperatureOffset { get; set; } = 0f;

        public SensorTemperature()
        {
            Icon     = "thermostat";
            Tickrate = int.MaxValue;
            Settings
                .Add(nameof(TemperatureOffset), () => TemperatureOffset, v => TemperatureOffset = v)
                    .Label("pl", "Offset temperatury (°C)").Label("en", "Temperature offset (°C)")
                    .Range(-10, 10);
        }

        protected override void Update(Stopwatch sw) { }

        public override void Draw(Stopwatch sw)
        {
            Screen.SetText("C*", Color.Green, 1);
            float t = SensorService.RawTemperature + TemperatureOffset;
            Screen.SetText(SensorService.HasError ? "---" : t.ToString("#.#"), Color.DarkRed, 10);
        }
    }

    public class SensorHumidity : PixelModule
    {
        static readonly Image _icon = Image.FromStream(
            Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelSensor.Humidity.png"));

        public SensorHumidity() { Icon = "water_drop"; Tickrate = int.MaxValue; }

        protected override void Update(Stopwatch sw) { }

        public override void Draw(Stopwatch sw)
        {
            Screen.SetImage(_icon, 0);
            if (SensorService.HasError) { Screen.SetText("---", Color.DarkRed, 9); return; }
            Screen.SetText(((int)Math.Round(SensorService.RawHumidity)) + "%", Color.Cyan, 9);
        }
    }

    public enum PressureMode { Actual, SeaLevel }

    public class SensorPressure : PixelModule
    {
        public PressureMode PressureDisplay { get; set; } = PressureMode.Actual;
        public float        Altitude        { get; set; } = 0f;

        public SensorPressure()
        {
            Icon     = "compress";
            Tickrate = int.MaxValue;
            Settings
                .Add(nameof(PressureDisplay), () => PressureDisplay, v => PressureDisplay = v)
                    .Label("pl", "Tryb ciśnienia").Label("en", "Pressure mode")
                    .EnumLabel("pl", "Actual=Rzeczywiste", "SeaLevel=Poziom morza")
                    .EnumLabel("en", "Actual=Actual", "SeaLevel=Sea level")
                .Add(nameof(Altitude), () => Altitude, v => Altitude = v)
                    .Label("pl", "Wysokość n.p.m. (m)").Label("en", "Altitude (m)")
                    .Range(0, 3000).StepSize(1)
                    .When(() => PressureDisplay == PressureMode.SeaLevel);
        }

        protected override void Update(Stopwatch sw) { }

        public override void Draw(Stopwatch sw)
        {
            if (SensorService.HasError) { Screen.SetText("---", Color.DarkRed, 0); return; }
            float p = SensorService.RawPressure;
            if (PressureDisplay == PressureMode.SeaLevel && Altitude > 0)
                p = (float)(p * Math.Exp(Altitude / (29.3 * (273.15 + SensorService.RawTemperature))));
            int pos = Screen.SetText(((int)p).ToString(), Color.Orange, 0);
            Screen.SetText("hPa", Color.DarkOrange, pos, 0);
        }
    }
}
