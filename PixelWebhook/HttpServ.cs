using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace PixelWebhook
{
    public class WebhookEventArgs : EventArgs
    {
        public string Payload { get; set; }
    }

    class HttpServ
    {
        public static HttpListener listener;
        public static string url = "http://localhost:8000/";
        public static bool IsRunning = false;
        public static event EventHandler<WebhookEventArgs> WebhookReceived;

        public static async Task HandleIncomingConnections()
        {
            IsRunning = true;
            while (IsRunning)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/webhook")
                {
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        string payload = await reader.ReadToEndAsync();
                        WebhookReceived?.Invoke(null, new WebhookEventArgs { Payload = payload });
                    }
                    resp.StatusCode = 200;
                    byte[] data = Encoding.UTF8.GetBytes("OK");
                    resp.ContentType = "text/plain";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);
                    resp.Close();
                    continue;
                }

                // Default response for other requests
                byte[] defaultData = Encoding.UTF8.GetBytes("PixelWebhook HTTP Server");
                resp.ContentType = "text/plain";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = defaultData.LongLength;
                await resp.OutputStream.WriteAsync(defaultData, 0, defaultData.Length);
                resp.Close();
            }
        }

        public static void Stop()
        {
            IsRunning = false;
            if (listener != null && listener.IsListening)
            {
                listener.Stop();
                listener.Close();
            }
        }
    }
}