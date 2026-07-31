using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace SharpClock
{
    partial class GPIO : IGPIO
    {
        internal static IGPIO GPIOevents { get; private set; }
        bool[] buttonsStatus = new bool[5];
        bool longButtonStatus = false;
        public event ButtonEventArgs OnButtonClick;
        IGpioPin buzzer;
        internal GPIO()
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
            buzzer = Pi.Gpio[HardwareConfig.BuzzerPin];
            buzzer.PinMode = GpioPinDriveMode.Output;

            //buttons
            IGpioPin[] buttons = { Pi.Gpio[HardwareConfig.BtnPause], Pi.Gpio[HardwareConfig.BtnNext], Pi.Gpio[HardwareConfig.BtnUser1], Pi.Gpio[HardwareConfig.BtnUser2], Pi.Gpio[HardwareConfig.BtnUser3] };

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
                      try
                      {
                        if (button.Value)
                        {
                            int pin = button.BcmPinNumber;
                            if (pin == HardwareConfig.BtnPause) {
                                if (!buttonsStatus[0]) OnButtonClick?.Invoke(ButtonId.Pause);
                                buttonsStatus[0] = true;
                            } else if (pin == HardwareConfig.BtnNext) {
                                nextLongPressTimer.Start();
                                if (!buttonsStatus[1]) OnButtonClick?.Invoke(ButtonId.Next);
                                if (!longButtonStatus && nextLongPressTimer.ElapsedMilliseconds >= 3000) {
                                    OnButtonClick?.Invoke(ButtonId.LongNext);
                                    longButtonStatus = true;
                                }
                                buttonsStatus[1] = true;
                            } else if (pin == HardwareConfig.BtnUser1) {
                                if (!buttonsStatus[2]) OnButtonClick?.Invoke(ButtonId.User1);
                                buttonsStatus[2] = true;
                            } else if (pin == HardwareConfig.BtnUser2) {
                                if (!buttonsStatus[3]) OnButtonClick?.Invoke(ButtonId.User2);
                                buttonsStatus[3] = true;
                            } else if (pin == HardwareConfig.BtnUser3) {
                                if (!buttonsStatus[4]) OnButtonClick?.Invoke(ButtonId.User3);
                                buttonsStatus[4] = true;
                            }
                        }
                        else
                        {
                            int pin = button.BcmPinNumber;
                            if (pin == HardwareConfig.BtnPause)  { buttonsStatus[0] = false; }
                            else if (pin == HardwareConfig.BtnNext)  { nextLongPressTimer.Reset(); buttonsStatus[1] = false; longButtonStatus = false; }
                            else if (pin == HardwareConfig.BtnUser1) { buttonsStatus[2] = false; }
                            else if (pin == HardwareConfig.BtnUser2) { buttonsStatus[3] = false; }
                            else if (pin == HardwareConfig.BtnUser3) { buttonsStatus[4] = false; }
                        }
                      }
                      catch (Exception e)
                      {
                        // Without this a single handler exception killed this button's polling
                        // loop for the rest of the process lifetime.
                        Logger.Log(ConsoleColor.Red, $"[GPIO] button loop: {e.GetType().Name}: {e.Message}");
                      }
                      await Task.Delay(100);
                    }
                });
                ButtonTask.Start();
            }
        }

        int _buzzing;
        public void EnableBuzzer(int amount = 3, int howLong = 100, int interval = 200)
        {
            // One buzzer thread at a time. Overlapping calls used to stack up threads that
            // fought over the same pin; a stuck caller could spawn one per second indefinitely.
            if (Interlocked.CompareExchange(ref _buzzing, 1, 0) != 0) return;
            new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < amount; i++)
                    {
                        buzzer.Write(GpioPinValue.High);
                        Thread.Sleep(howLong);
                        buzzer.Write(GpioPinValue.Low);
                        Thread.Sleep(interval);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.Red, $"[GPIO] buzzer: {e.Message}");
                }
                finally
                {
                    buzzer.Write(GpioPinValue.Low);
                    Interlocked.Exchange(ref _buzzing, 0);
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
