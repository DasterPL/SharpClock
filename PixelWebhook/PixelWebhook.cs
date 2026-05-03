using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Net;
using SharpClock;

namespace PixelWebhook
{
    public class PixelWebhook : PixelModule
    {
        public string Text { get; set; } = "Sharp Clock";
        public bool Pause { get; set; } = false;
        public Color Color { get; set; } = Color.White;
        public int Speed { get => Tickrate; set => Tickrate = value; }

        private bool _webhookEnabled = false;
        public bool WebhookEnabled
        {
            get => _webhookEnabled;
            set
            {
                if (_webhookEnabled != value)
                {
                    _webhookEnabled = value;
                    TryStartOrStopServer();
                }
            }
        }

        private int _webhookPort = 3001;
        public int WebhookPort
        {
            get => _webhookPort;
            set
            {
                if (_webhookPort != value)
                {
                    _webhookPort = value;
                    TryStartOrStopServer();
                }
            }
        }

        private int pos = 2;
        private Task httpTask;
        private static object httpLock = new object();

        public PixelWebhook()
        {
            Icon = "message";
            HttpServ.WebhookReceived += OnWebhookReceived;
            TryStartOrStopServer();

            Settings
                .Add(nameof(Text), () => Text, v => Text = v)
                    .Label("pl", "Tekst").Label("en", "Text").Multiline()
                .Add(nameof(Pause), () => Pause, v => Pause = v)
                    .Label("pl", "Wstrzymaj").Label("en", "Pause")
                .Add(nameof(Color), () => Color, v => Color = v)
                    .Label("pl", "Kolor tekstu").Label("en", "Text Color")
                .Add(nameof(Speed), () => Speed, v => Speed = v)
                    .Label("pl", "Szybkość Przewijania").Label("en", "Scroll speed").StepSize(10)
                .Add(nameof(WebhookEnabled), () => WebhookEnabled, v => WebhookEnabled = v)
                    .Label("pl", "Webhook włączony").Label("en", "Webhook enabled")
                .Add(nameof(WebhookPort), () => WebhookPort, v => WebhookPort = v)
                    .Label("pl", "Webhook Port").Label("en", "Webhook Port")
                    .Range(1, 65535)
                    .When(() => WebhookEnabled);
        }

        private void TryStartOrStopServer()
        {
            lock (httpLock)
            {
                if (WebhookEnabled)
                {
                    if (HttpServ.listener == null || !HttpServ.listener.IsListening)
                    {
                        HttpServ.url = $"http://+:{WebhookPort}/webhook/";
                        HttpServ.listener = new HttpListener();
                        HttpServ.listener.Prefixes.Clear();
                        HttpServ.listener.Prefixes.Add($"http://+:{WebhookPort}/webhook/");
                        HttpServ.listener.Start();
                        httpTask = Task.Run(() => HttpServ.HandleIncomingConnections());
                    }
                }
                else
                {
                    if (HttpServ.listener != null && HttpServ.listener.IsListening)
                        HttpServ.Stop();
                }
            }
        }

        private void OnWebhookReceived(object sender, WebhookEventArgs e)
        {
            string orderNumber = "";
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(e.Payload, "\\\"id\\\"\\s*:\\s*(\\d+)");
                if (match.Success)
                    orderNumber = match.Groups[1].Value;
            }
            catch { }
            Text = !string.IsNullOrEmpty(orderNumber) ? $"Nowe zamówienie #{orderNumber}" : "Nowe zamówienie";
            Visible = true;
            pixelRenderer?.SwitchModule(this);
            GPIOevents?.EnableBuzzer();
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 30000)
                    Task.Delay(500).Wait();
                Visible = false;
            });
        }

        public override void Draw(Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < Tickrate)
                pos = 1;
            Screen.SetText(Text, Color, pos);
        }

        protected override void Update(Stopwatch stopwatch)
        {
            int len = Screen.TextLength(Text);

            if (!Pause)
            {
                if (len <= 30)
                {
                    pos = 1;
                }
                else
                {
                    if (pos == 2)
                        Tickrate = 1500;
                    else
                        Tickrate = 160;
                    if (pos-- <= -len)
                        pos = 31;
                }
                Timer = (len * Tickrate * 2) + (len < 32 ? 32 - len : 0) * Tickrate;
            }
        }

        public override void OnButtonClick(ButtonId button)
        {
            base.OnButtonClick(button);
            if (button == ButtonId.User1)
                Pause = !Pause;
            else if (button == ButtonId.User2)
                Speed += 10;
            else if (button == ButtonId.User3)
                Speed -= 10;
            pixelRenderer.UpdateConfig(this);
        }
    }
}
