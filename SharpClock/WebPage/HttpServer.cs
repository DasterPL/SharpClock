using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SharpClock
{
    class HttpServer
    {
        bool runServer = true;
        public bool IsReady { get; private set; } = false;

        public delegate JToken ApiRequest(string method, string path, System.Collections.Specialized.NameValueCollection query);
        public event ApiRequest OnRequest;

        public delegate void DelegateError(string error);
        public event DelegateError OnError;

        public HttpServer(string pageDirectory)
        {
            this.pageDirectory = pageDirectory;
        }

        HttpListener listener = new HttpListener();
        string pageDirectory;

        static readonly System.Collections.Generic.HashSet<string> SilentPaths =
            new System.Collections.Generic.HashSet<string> { "/screen", "/log" };

        private async Task HandleIncomingConnections()
        {
            while (runServer)
            {
                try
                {
                    HttpListenerContext ctx = await listener.GetContextAsync();
                    _ = HandleRequest(ctx);
                }
                catch when (!runServer)
                {
                    break;
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            try
            {
                string method = req.HttpMethod;
                string absPath = req.Url.AbsolutePath;

                if (method == "GET" && (absPath == "/" || System.IO.Path.HasExtension(absPath)))
                {
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $"GET {absPath}");
                    string filePath = absPath == "/" ? "/index.html" : absPath;
                    try
                    {
                        resp.ContentType = GetMimeType(filePath);
                        byte[] file = File.ReadAllBytes(pageDirectory + filePath);
                        resp.ContentEncoding = Encoding.UTF8;
                        resp.ContentLength64 = file.LongLength;
                        await resp.OutputStream.WriteAsync(file, 0, file.Length);
                        resp.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.Red, "[Error]", ConsoleColor.White, e.Message);
                        if (e is FileNotFoundException || e is DirectoryNotFoundException)
                        {
                            byte[] data = Encoding.UTF8.GetBytes("<h1>HTTP Error 404 - File Not Found</h1>");
                            resp.ContentType = "text/html";
                            resp.ContentEncoding = Encoding.UTF8;
                            resp.ContentLength64 = data.LongLength;
                            await resp.OutputStream.WriteAsync(data, 0, data.Length);
                            resp.Close();
                        }
                    }
                    return;
                }

                // API request
                bool silent = SilentPaths.Contains(absPath);
                JToken apiResult = null;
                System.Collections.Specialized.NameValueCollection query;

                if (req.ContentType?.StartsWith("multipart/form-data;") == true)
                {
                    Stream tmp = new MemoryStream();
                    req.InputStream.CopyTo(tmp);

                    query = new System.Collections.Specialized.NameValueCollection();
                    tmp.Position = 0;
                    var reader = new StreamReader(tmp, req.ContentEncoding);
                    reader.ReadLine();
                    query.Add("fileName", Regex.Match(reader.ReadLine(), "filename=\"(.+?)\"").Groups[1].Value);
                    query.Add("ContentType", reader.ReadLine().Split(':')[1].Substring(1));
                    tmp.Position = 0;

                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White,
                        $"{method} {absPath}: File={query["fileName"]}");

                    apiResult = OnRequest?.Invoke(method, absPath, query);

                    if (apiResult?["result"] != null)
                        POST_File.SaveFile(apiResult["result"].ToString(), tmp, req.ContentEncoding, req.ContentType);

                    reader.Dispose();
                }
                else if (method == "GET" || method == "DELETE")
                {
                    query = HttpUtility.ParseQueryString(req.Url.Query);
                    if (!silent)
                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White,
                            $"{method} {absPath}{req.Url.Query}");
                    apiResult = OnRequest?.Invoke(method, absPath, query);
                }
                else
                {
                    var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    query = HttpUtility.ParseQueryString(reader.ReadToEnd());
                    if (!silent)
                    {
                        string ps = "";
                        foreach (string key in query.Keys)
                            ps += $"{key}={query[key]}; ";
                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White,
                            $"{method} {absPath}: {ps}");
                    }
                    apiResult = OnRequest?.Invoke(method, absPath, query);
                }

                dynamic json = new JObject();
                json.HostTime = DateTime.Now.ToString();
                json.Response = apiResult;
                byte[] responseData = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = responseData.LongLength;
                await resp.OutputStream.WriteAsync(responseData, 0, responseData.Length);
                resp.Close();
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.Red, "[Error] ", ConsoleColor.White, $"[{e.GetType()}] {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        private static string GetMimeType(string path)
        {
            switch (System.IO.Path.GetExtension(path))
            {
                case ".js":   return "application/javascript";
                case ".json": return "application/json";
                case ".ogg":  return "application/ogg";
                case ".mp3":  return "audio/mpeg";
                case ".gif":  return "image/gif";
                case ".jpeg":
                case ".jpg":  return "image/jpeg";
                case ".png":  return "image/png";
                case ".tif":
                case ".tiff": return "image/tiff";
                case ".ico":  return "image/vnd.microsoft.icon";
                case ".css":  return "text/css";
                case ".html":
                case ".htm":  return "text/html";
                case ".txt":
                case ".log":  return "text/plain";
                case ".xml":  return "text/xml";
                case ".mp4":  return "video/mp4";
                default:      return "application/octet-stream";
            }
        }

        public void Start(int Port = 80)
        {
            listener.Prefixes.Add($"http://*:{Port}/");
            new Task(() =>
            {
                try
                {
                    listener.Start();
                    IsReady = true;
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $" Listening for connections on port: {Port}");
                    HandleIncomingConnections().GetAwaiter().GetResult();
                    listener.Close();
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.Red, "[Error] ", ConsoleColor.White, $"[{e.GetType()}] {e.Message}");
                    OnError?.Invoke(e.Message);
                }
            }).Start();
        }

        public void Stop()
        {
            Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, " Shutting down");
            runServer = false;
            listener.Close();
        }

        public bool IsRunning { get => runServer; }
    }
}
