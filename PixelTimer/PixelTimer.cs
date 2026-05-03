using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using SharpClock;

namespace PixelTimer
{
    public class PixelTimer : PixelModule
    {
        public enum TimerMode { Stopwatch, Countdown }

        public TimerMode Mode { get; set; } = TimerMode.Stopwatch;
        public TimeSpan CountdownFrom { get; set; } = new TimeSpan(0, 5, 0);

        int _displayTimer = 10000;

        readonly object _sync = new object();
        readonly Stopwatch _clock = new Stopwatch();
        bool _running;
        TimeSpan _elapsed = TimeSpan.Zero;
        bool _buzzed;

        public PixelTimer()
        {
            Icon = "timer";
            Timer = 10000;
            Tickrate = 100;

            Settings.All.FirstOrDefault(e => e.Key == nameof(Timer))?.When(() => false);

            Settings
                .Add("DisplayTimer", () => _displayTimer, v => _displayTimer = v)
                    .Label("pl", "Czas wyświetlania").Label("en", "Display time")
                .Add(nameof(Mode), () => Mode, v => Mode = v)
                    .Label("pl", "Tryb").Label("en", "Mode")
                    .EnumLabel("pl", "Stopwatch=Stoper", "Countdown=Odliczanie")
                    .EnumLabel("en", "Stopwatch=Stopwatch", "Countdown=Countdown")
                .Add(nameof(CountdownFrom), () => CountdownFrom, v => CountdownFrom = v)
                    .Label("pl", "Odliczaj od").Label("en", "Countdown from")
                    .When(() => Mode == TimerMode.Countdown);
        }

        // Must be called under _sync
        TimeSpan GetElapsed() =>
            _elapsed + (_running ? _clock.Elapsed : TimeSpan.Zero);

        // Must be called under _sync
        TimeSpan CurrentTime()
        {
            var e = GetElapsed();
            if (Mode == TimerMode.Countdown)
            {
                var r = CountdownFrom - e;
                return r < TimeSpan.Zero ? TimeSpan.Zero : r;
            }
            return e;
        }

        public override void Draw(Stopwatch _ignored)
        {
            TimeSpan time;
            bool running, buzzed;
            lock (_sync)
            {
                time    = CurrentTime();
                running = _running;
                buzzed  = _buzzed;
            }

            int totalSec = (int)time.TotalSeconds;
            Screen.SetTextCentered(
                $"{totalSec / 60:D2}:{totalSec % 60:D2}",
                running ? Color.Lime
                    : (Mode == TimerMode.Countdown && buzzed)
                        ? (DateTime.Now.Millisecond < 500 ? Color.Red : Color.DarkRed)
                        : Color.DodgerBlue,
                0);
        }

        protected override void Update(Stopwatch _ignored)
        {
            lock (_sync)
            {
                Timer = (_running || (Mode == TimerMode.Countdown && !_buzzed))
                    ? int.MaxValue : _displayTimer;

                if (Mode != TimerMode.Countdown || !_running || _buzzed) return;
                if (GetElapsed() >= CountdownFrom)
                {
                    _elapsed = CountdownFrom;
                    _clock.Reset();
                    _running = false;
                    _buzzed  = true;
                    GPIOevents?.EnableBuzzer(9);
                }
            }
        }

        bool StartStop()
        {
            bool switchToThis = false;
            lock (_sync)
            {
                if (_running)
                {
                    _elapsed += _clock.Elapsed;
                    _clock.Reset();
                    _running = false;
                }
                else
                {
                    if (_buzzed) ResetUnsafe();
                    _clock.Restart();
                    _running      = true;
                    _buzzed       = false;
                    switchToThis  = true;
                }
            }
            return switchToThis;
        }

        void ResetUnsafe()
        {
            _running = false;
            _elapsed = TimeSpan.Zero;
            _clock.Reset();
            _buzzed  = false;
        }

        void Reset()
        {
            lock (_sync) { ResetUnsafe(); }
        }

        public override IDictionary<string, object> GetState()
        {
            lock (_sync)
                return new Dictionary<string, object>
                {
                    ["running"] = _running,
                    ["buzzed"]  = _buzzed,
                    ["mode"]    = Mode.ToString(),
                };
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            switch (button)
            {
                case ButtonId.User1:
                    if (StartStop())
                        pixelRenderer.SwitchModule(this, forcePause: true);
                    break;
                case ButtonId.User2: Reset(); break;
                case ButtonId.User3:
                    Reset();
                    Mode = Mode == TimerMode.Stopwatch ? TimerMode.Countdown : TimerMode.Stopwatch;
                    break;
            }
            pixelRenderer.UpdateConfig(this);
        }
    }
}
