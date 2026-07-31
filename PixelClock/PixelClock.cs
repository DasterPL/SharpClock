using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using SharpClock;

namespace PixelClock
{
    public class PixelClock : PixelModule
    {
        public bool ShowSeconds { get; set; } = false;
        public TimeSpan Alarm { get; set; } = new TimeSpan(0, 0, 0);
        public bool EnableTimer { get; set; } = false;

        DateTime Date;
        GifImage clockImage;
        GifImage alarmImage;
        TimeSpan showAlarmScreen = new TimeSpan(0, 0, 0);
        bool playAlarm = false;

        // The alarm rings for a bounded time and buzzes on an interval. Previously playAlarm
        // was latched on and never cleared, so Update() spawned a buzzer thread every second
        // and pinned the clock on screen until the next restart.
        const int AlarmDurationMs = 60_000;
        const int AlarmBuzzEveryMs = 5_000;
        DateTime lastAlarmFired = DateTime.MinValue;
        Stopwatch alarmSince;
        long lastBuzzMs;

        public PixelClock()
        {
            Icon = "access_time";
            clockImage = new GifImage(Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream($"PixelClock.Clock.clock.gif")));
            alarmImage = new GifImage(Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream($"PixelClock.Clock.alarm.gif")));

            Settings
                .Add(nameof(ShowSeconds), () => ShowSeconds, v => ShowSeconds = v)
                    .Label("pl", "Pokaż Sekundy").Label("en", "Show Seconds")
                .Add(nameof(Alarm), () => Alarm, v => Alarm = v)
                    .Label("pl", "Budzik").Label("en", "Alarm")
                .Add(nameof(EnableTimer), () => EnableTimer, v => EnableTimer = v)
                    .Label("pl", "Włącz Budzik").Label("en", "Enable Alarm");
        }

        protected override void Update(Stopwatch stopwatch)
        {
            Date = DateTime.Now;

            // Arm once per occurrence — the minute-match stays true for a whole minute.
            if (EnableTimer && Alarm.Hours == Date.Hour && Alarm.Minutes == Date.Minute
                && (Date - lastAlarmFired).TotalMinutes >= 2)
            {
                lastAlarmFired = Date;
                alarmSince = Stopwatch.StartNew();
                lastBuzzMs = -AlarmBuzzEveryMs;
                playAlarm = true;
            }

            if (!playAlarm) return;

            if (alarmSince == null || alarmSince.ElapsedMilliseconds > AlarmDurationMs)
            {
                StopAlarm();
                return;
            }

            Visible = true;
            pixelRenderer?.SwitchModule(this);
            if (alarmSince.ElapsedMilliseconds - lastBuzzMs >= AlarmBuzzEveryMs)
            {
                lastBuzzMs = alarmSince.ElapsedMilliseconds;
                GPIOevents?.EnableBuzzer();
            }
        }

        void StopAlarm()
        {
            playAlarm = false;
            alarmSince = null;
        }

        public override void Draw(Stopwatch stopwatch)
        {
            if (showAlarmScreen.Seconds > 0)
            {
                Screen.SetImage(alarmImage.Advance(), 0);
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
                    Screen.SetImage(clockImage.Advance(), 0);
                    Screen.SetText(Date.Hour.ToString("D2"), Color.DarkRed, 10);
                    if (Date.Second % 2 == 0) Screen.SetText(":", Color.DarkBlue, 20);
                    Screen.SetText(Date.Minute.ToString("D2"), Color.DarkRed, 22);
                }
                if (EnableTimer)
                    Screen.SetPixel(new Point(31, 7), Color.DarkBlue);
            }
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            if (button == ButtonId.User1)
            {
                ShowSeconds = !ShowSeconds;
            }
            else if (button == ButtonId.User3)
            {
                EnableTimer = !EnableTimer;
                StopAlarm();
                showAlarmScreen = EnableTimer ? new TimeSpan(0, 0, 2) : new TimeSpan(0, 0, 0);
                new System.Threading.Tasks.Task(async () =>
                {
                    while (showAlarmScreen.Seconds > 0)
                    {
                        await System.Threading.Tasks.Task.Delay(1000);
                        showAlarmScreen = showAlarmScreen.Subtract(new TimeSpan(0, 0, 1));
                    }
                }).Start();
            }
            pixelRenderer.UpdateConfig(this);
        }
    }
}
