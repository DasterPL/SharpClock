using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace SharpClock
{
    partial class GPIO : IGPIO
    {
        public static IGPIO GPIOevents { get; private set; }
        bool[] buttonsStatus = new bool[5];
        bool longButtonStatus = false;
        public event ButtonEventArgs OnButtonClick;
        IGpioPin buzzer;
        public GPIO()
        {
            if(GPIOevents == null)
            {
                GPIOevents = this;
            }
            else
            {
                throw new Exception("Can't create more GPIO Controler");
            }

            Pi.Init<BootstrapWiringPi>();

            //Buzzer
            buzzer = Pi.Gpio[25];
            buzzer.PinMode = GpioPinDriveMode.Output;

            //buttons
            IGpioPin[] buttons = { Pi.Gpio[17], Pi.Gpio[27], Pi.Gpio[22], Pi.Gpio[23], Pi.Gpio[24] };

            for (int i = 0; i < buttonsStatus.Length; i++)
            {
                buttonsStatus[i] = false;
            }
            var nextLongPressTimer = new Stopwatch();
            foreach (var button in buttons)
            {
                button.PinMode = GpioPinDriveMode.Input;

                var ButtonTask = new Task(async () =>
                {
                    
                    while (true)
                    {

                        if (button.Value)
                        {
                            switch (button.BcmPinNumber)
                            {
                                case 17:
                                    if (!buttonsStatus[0])
                                        OnButtonClick?.Invoke(ButtonId.Pause);
                                    buttonsStatus[0] = true;
                                    break;
                                case 27:
                                    nextLongPressTimer.Start();
                                    if (!buttonsStatus[1])
                                        OnButtonClick?.Invoke(ButtonId.Next);
                                    if (!longButtonStatus && nextLongPressTimer.ElapsedMilliseconds >= 3000) 
                                    {
                                        OnButtonClick?.Invoke(ButtonId.LongNext);
                                        longButtonStatus = true;
                                    }
                                    buttonsStatus[1] = true;
                                    break;
                                case 22:
                                    if (!buttonsStatus[2])
                                        OnButtonClick?.Invoke(ButtonId.User1);
                                    buttonsStatus[2] = true;
                                    break;
                                case 23:
                                    if (!buttonsStatus[3])
                                        OnButtonClick?.Invoke(ButtonId.User2);
                                    buttonsStatus[3] = true;
                                    break;
                                case 24:
                                    if (!buttonsStatus[4])
                                        OnButtonClick?.Invoke(ButtonId.User3);
                                    buttonsStatus[4] = true;
                                    break;
                            }
                        }
                        else
                        {
                            switch (button.BcmPinNumber)
                            {
                                case 17:
                                    buttonsStatus[0] = false;
                                    break;
                                case 27:
                                    nextLongPressTimer.Reset();
                                    buttonsStatus[1] = false;
                                    longButtonStatus = false;
                                    break;
                                case 22:
                                    buttonsStatus[2] = false;
                                    break;
                                case 23:
                                    buttonsStatus[3] = false;
                                    break;
                                case 24:
                                    buttonsStatus[4] = false;
                                    break;
                            }
                        }
                        await Task.Delay(100);
                    }
                });
                ButtonTask.Start();
            }
            //Bluetooth
            //var bt = new Bluetooth();
        }

        public void EnableBuzzer(int amount = 3, int howLong = 100, int interval = 200)
        {
            new Thread(() =>
            {
                for (int i = 0; i < amount; i++)
                {
                    buzzer.Write(GpioPinValue.High);
                    Thread.Sleep(howLong);
                    buzzer.Write(GpioPinValue.Low);
                    Thread.Sleep(interval);
                }
            }).Start();
        }
    }
}
