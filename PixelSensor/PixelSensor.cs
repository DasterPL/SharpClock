using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using SharpClock;

namespace PixelSensor
{
    public class PixelSensor : PixelModule
    {
        BME280 sensor;
        float temperature = 0;
        public PixelSensor()
        {
            Icon = "ac_unit";
            //BMP280 sensor
            try
            {
                sensor = new BME280();
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, e.Message);
            }
            //Tickrate = 10000;
        }
        public override void Draw(Stopwatch stopwatch)
        {
            Screen.SetText("C*", Color.Green, 1);
            Screen.SetText(temperature.ToString("#.#"), Color.DarkRed, 10);
        }

        protected override void Update(Stopwatch stopwatch)
        {
            try
            {
                temperature = sensor.ReadTemperature();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
