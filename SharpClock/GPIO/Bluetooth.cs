using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace SharpClock
{
    class Bluetooth
    {
        Unosquare.RaspberryIO.Computer.Bluetooth bluetooth;
        public Bluetooth()
        {
            Init();
        }
        async void Init()
        {
            bluetooth = Unosquare.RaspberryIO.Computer.Bluetooth.Instance;
            await bluetooth.PowerOn();
            var s = (await bluetooth.ListControllers()).ToArray();
            foreach (var item in s)
            {
                Console.WriteLine(item);
            }
            //bluetooth.
        }
    }
}
