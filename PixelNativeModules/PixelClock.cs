using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using SharpClock;

namespace PixelNativeModules
{
    public class PixelClock : PixelModule
    {
        DateTime Date;

        public bool ShowSeconds { get; set; } = false;
        
        GifImage clockImage;
        GifImage alarmImage;

        public TimeSpan Alarm { get; set; } = new TimeSpan(0, 0, 0);
        TimeSpan showAlarmScreen = new TimeSpan(0, 0, 0);
        bool playAlarm = false;

        public bool EnableTimer { get; set; } = false;

        public PixelClock()
        {
            Icon = "access_time";
            var assembly = Assembly.GetExecutingAssembly();

            clockImage = new GifImage(Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream($"PixelNativeModules.Clock.clock.gif")));
            alarmImage = new GifImage(Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream($"PixelNativeModules.Clock.alarm.gif")));
        }
        protected override void Update(Stopwatch stopwatch)
        {
            Date = DateTime.Now;

            if (EnableTimer && Alarm.Hours == Date.Hour && Alarm.Minutes == Date.Minute)
            {
                playAlarm = true;
            }
            if (playAlarm)
            {
                pixelRenderer.SwitchModule(this);
                GPIOevents.EnableBuzzer();
            }

        }
        public override void Draw(Stopwatch stopwatch)
        {
            if(showAlarmScreen.Seconds > 0)
            {
                Screen.SetImage(alarmImage.GetCurrentFrame, 0);

                Screen.SetText(Alarm.Hours.ToString("D2"), Color.DarkRed, 10);
                if (Date.Second % 2 == 0) Screen.SetText(":", Color.Yellow, 20);
                Screen.SetText(Alarm.Minutes.ToString("D2"), Color.DarkRed, 22);
            }
            else
            {
                if (ShowSeconds)
                {
                    Screen.SetText(Date.Hour.ToString("D2"), Color.DarkRed, 1);
                    if (Date.Second % 2 == 0) Screen.SetText(":", Color.DarkBlue, 10);
                    Screen.SetText(Date.Minute.ToString("D2"), Color.DarkRed, 11);
                    if (Date.Second % 2 == 0) Screen.SetText(":", Color.DarkBlue, 21);
                    Screen.SetText(Date.Second.ToString("D2"), Color.Orange, 23);
                }
                else
                {
                    Screen.SetImage(clockImage.GetCurrentFrame, 0);

                    Screen.SetText(Date.Hour.ToString("D2"), Color.DarkRed, 10);
                    if (Date.Second % 2 == 0) Screen.SetText(":", Color.DarkBlue, 20);
                    Screen.SetText(Date.Minute.ToString("D2"), Color.DarkRed, 22);
                }
                if (EnableTimer)
                {
                    Screen.SetPixel(new Point(31, 7), Color.DarkBlue);
                }
            }
           
        }
        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            if(button == ButtonId.User1)
            {
                ShowSeconds = !ShowSeconds;
            }
            else if(button == ButtonId.User3)
            {
                EnableTimer = !EnableTimer;
                playAlarm = false;
                showAlarmScreen = EnableTimer ? new TimeSpan(0, 0, 2) : new TimeSpan(0, 0, 0);
                new System.Threading.Tasks.Task(async()=> {
                    while (showAlarmScreen.Seconds > 0)
                    {
                        await System.Threading.Tasks.Task.Delay(1000);
                        showAlarmScreen = showAlarmScreen.Subtract(new TimeSpan(0, 0, 1));
                    }
                }).Start();
            }
            pixelRenderer.UpdateConfig();
        }
    }
}
