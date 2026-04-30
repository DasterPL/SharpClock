using System;
using System.Diagnostics;
using System.Drawing;
using SharpClock;

namespace PixelSensor
{
    public class PixelSensor : PixelModule
    {
        public enum SubModule { Temperature, Humidity, Pressure }
        public enum DisplayMode { Static, Rotating }
        public enum PressureMode { Actual, SeaLevel }

        public DisplayMode Mode { get; set; } = DisplayMode.Rotating;
        public int RotationInterval { get; set; } = 5000;
        public SubModule ModuleToShow { get; set; } = SubModule.Temperature;
        public PressureMode PressureDisplay { get; set; } = PressureMode.Actual;
        public float Altitude { get; set; } = 0f;
        public float TemperatureOffset { get; set; } = 0f;

        BME280 sensor;
        float temperature = 0;
        float humidity = 0;
        float pressure = 0;
        bool _sensorError = false;

        long _prevStopwatchMs = -1;
        long _accSubModuleMs = 0;
        const int TransitionMs = 266;

        public PixelSensor()
        {
            Icon = "ac_unit";
            try { sensor = new BME280(); }
            catch (Exception e) { Logger.Log(ConsoleColor.Red, e.Message); }

            Settings
                .Add(nameof(Mode), () => Mode, v => Mode = v)
                    .Label("pl", "Tryb").Label("en", "Mode")
                    .EnumLabel("pl", "Static=Stały", "Rotating=Zmienny")
                    .EnumLabel("en", "Static=Static", "Rotating=Rotating")
                .Add(nameof(RotationInterval), () => RotationInterval, v => RotationInterval = v)
                    .Label("pl", "Czas zmiany (ms)").Label("en", "Rotation interval (ms)")
                    .Range(100, 30000)
                    .When(() => Mode == DisplayMode.Rotating)
                .Add(nameof(ModuleToShow), () => ModuleToShow, v => ModuleToShow = v)
                    .Label("pl", "Pokaż").Label("en", "Show")
                    .EnumLabel("pl", "Temperature=Temperatura", "Humidity=Wilgotność", "Pressure=Ciśnienie")
                    .EnumLabel("en", "Temperature=Temperature", "Humidity=Humidity", "Pressure=Pressure")
                    .When(() => Mode == DisplayMode.Static)
                .Add(nameof(PressureDisplay), () => PressureDisplay, v => PressureDisplay = v)
                    .Label("pl", "Tryb ciśnienia").Label("en", "Pressure mode")
                    .EnumLabel("pl", "Actual=Rzeczywiste", "SeaLevel=Poziom morza")
                    .EnumLabel("en", "Actual=Actual", "SeaLevel=Sea level")
                .Add(nameof(Altitude), () => Altitude, v => Altitude = v)
                    .Label("pl", "Wysokość n.p.m. (m)").Label("en", "Altitude (m)")
                    .Range(0, 3000).StepSize(1)
                    .When(() => PressureDisplay == PressureMode.SeaLevel)
                .Add(nameof(TemperatureOffset), () => TemperatureOffset, v => TemperatureOffset = v)
                    .Label("pl", "Offset temperatury (°C)").Label("en", "Temperature offset (°C)")
                    .Range(-10, 10);
        }

        void DrawTemperature()
        {
            Screen.SetText("C*", Color.Green, 1);
            Screen.SetText(_sensorError ? "---" : temperature.ToString("#.#"), Color.DarkRed, 10);
        }

        void DrawHumidity()
        {
            if (_sensorError) { Screen.SetText("---", Color.DarkRed, 0); return; }
            string val = ((int)Math.Round(humidity)).ToString();
            int start = (32 - Screen.TextLength(val + "%")) / 2;
            int pos = Screen.SetText(val, Color.Cyan, start);
            Screen.SetText("%", Color.DodgerBlue, pos);
        }

        void DrawPressure()
        {
            if (_sensorError) { Screen.SetText("---", Color.DarkRed, 0); return; }
            int pos = Screen.SetText(((int)pressure).ToString(), Color.Orange, 0);
            Screen.SetText("hPa", Color.DarkOrange, pos, 0);
        }

        void DrawSubModule(SubModule sub)
        {
            switch (sub)
            {
                case SubModule.Temperature: DrawTemperature(); break;
                case SubModule.Humidity:    DrawHumidity();    break;
                case SubModule.Pressure:    DrawPressure();    break;
            }
        }

        public override void Draw(Stopwatch stopwatch)
        {
            if (Mode == DisplayMode.Static || stopwatch == null)
            {
                DrawSubModule(ModuleToShow);
                return;
            }

            long cur = stopwatch.ElapsedMilliseconds;
            if (_prevStopwatchMs >= 0 && cur >= _prevStopwatchMs)
                _accSubModuleMs += cur - _prevStopwatchMs;
            _prevStopwatchMs = cur;

            var values = (SubModule[])Enum.GetValues(typeof(SubModule));
            int currentIdx = (int)(_accSubModuleMs / RotationInterval % values.Length);
            long timeUntilNext = RotationInterval - _accSubModuleMs % RotationInterval;

            if (timeUntilNext > TransitionMs)
            {
                DrawSubModule(values[currentIdx]);
                return;
            }

            int nextIdx = (currentIdx + 1) % values.Length;
            int xOff = Math.Min((int)((TransitionMs - timeUntilNext) * 32 / TransitionMs) + 1, 32);

            DrawSubModule(values[currentIdx]);
            var currentBuffer = Screen.GetBuffer();
            Screen.Clear();
            DrawSubModule(values[nextIdx]);
            var nextBuffer = Screen.GetBuffer();
            Screen.DrawFromBuffersX(currentBuffer, nextBuffer, xOff);
        }

        void NextSubModule()
        {
            var values = (SubModule[])Enum.GetValues(typeof(SubModule));
            if (Mode == DisplayMode.Static)
                ModuleToShow = values[((int)ModuleToShow + 1) % values.Length];
            else
                _accSubModuleMs = (_accSubModuleMs / RotationInterval + 1) * RotationInterval;
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            switch (button)
            {
                case ButtonId.User1: NextSubModule(); break;
                case ButtonId.User2:
                    Mode = Mode == DisplayMode.Static ? DisplayMode.Rotating : DisplayMode.Static;
                    break;
            }
            pixelRenderer.UpdateConfig();
        }

        protected override void Update(Stopwatch stopwatch)
        {
            try
            {
                temperature = sensor.ReadTemperature() + TemperatureOffset;
                humidity = sensor.ReadHumidity();
                pressure = sensor.ReadPreasure() / 100f;
                if (PressureDisplay == PressureMode.SeaLevel && Altitude > 0)
                    pressure = (float)(pressure * Math.Exp(Altitude / (29.3 * (273.15 + temperature))));
                _sensorError = false;
            }
            catch (Exception e)
            {
                _sensorError = true;
                Logger.Log(ConsoleColor.Blue, $"[{GetType().Name}]:", ConsoleColor.Red, e.Message);
            }
        }
    }
}
