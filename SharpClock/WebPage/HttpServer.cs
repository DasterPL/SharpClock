using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SharpClock
{
    class HttpServer
    {
        bool runServer = true;
        public bool IsReady { get; private set; } = false;

        public delegate void PostRequest(string path, System.Collections.Specialized.NameValueCollection query);
        public event PostRequest OnPost;
        public dynamic PostResponse { get; set; } = string.Empty;
        void Post(string path, System.Collections.Specialized.NameValueCollection query)
        {
            OnPost?.Invoke(path, query);
        }

        public delegate void DelegateError(string error);
        public event DelegateError OnError;
        void Error(string errorMessage)
        {
            OnError?.Invoke(errorMessage);
        }
        public HttpServer(string pageDirectory)
        {
            this.pageDirectory = pageDirectory;
        }
        HttpListener listener = new HttpListener();
        string pageDirectory;
        public async Task HandleIncomingConnections()
        {
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                if ((req.HttpMethod == "POST"))
                {
                    if (req.ContentType?.Substring(0, 20) == "multipart/form-data;")
                    {
                        Stream tmp = new MemoryStream();
                        req.InputStream.CopyTo(tmp);

                        var query = new System.Collections.Specialized.NameValueCollection();
                        tmp.Position = 0;
                        var reader = new StreamReader(tmp, req.ContentEncoding);
                        reader.ReadLine();
                        query.Add("fileName", Regex.Match(reader.ReadLine(), "filename=\"(.+?)\"").Groups[1].Value);
                        query.Add("ContentType", reader.ReadLine().Split(':')[1].Substring(1));         
                        tmp.Position = 0;

                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $"{req.HttpMethod} request on: {req.Url.AbsolutePath}: ", ConsoleColor.Green, "File", ConsoleColor.White, $"={query["fileName"]}");
                        Post(req.Url.AbsolutePath, query);

                        if (PostResponse != string.Empty)
                            POST_File.SaveFile(PostResponse, tmp, req.ContentEncoding, req.ContentType);
                        

                        reader.Dispose();
                    }
                    else
                    {
                        var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                        var  query = HttpUtility.ParseQueryString(reader.ReadToEnd());
                        string postParams = "";
                        foreach (string Key in query.Keys)
                        {
                            postParams += $"{Key} = {query[Key]}; ";
                        }
                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $"{req.HttpMethod} request on: {req.Url.AbsolutePath}: {postParams}");
                        Post(req.Url.AbsolutePath, query);
                    }
                    dynamic json = new JObject();
                    //json.Error = null;
                    json.HostTime = DateTime.Now.ToString();
                    json.Response = PostResponse != null ? PostResponse : null;
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server] Response: ", ConsoleColor.White, json.ToString());
                    byte[] data = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
                    PostResponse = string.Empty;
                    resp.ContentType = "application/json";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;

                    // Write out to the response stream (asynchronously), then close it
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);
                    resp.Close();
                }

                if ((req.HttpMethod == "GET"))
                {
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $"{req.HttpMethod} request on: {req.Url.AbsolutePath}");
                    string path = req.Url.AbsolutePath == "/" ? "/index.html" : req.Url.AbsolutePath;
                    try
                    {
                        switch (Path.GetExtension(path))
                        {
                            case ".js":
                                resp.ContentType = "application/javascript";
                                break;
                            case ".json":
                                resp.ContentType = "application/json";
                                break;
                            case ".ogg":
                                resp.ContentType = "application/ogg";
                                break;
                            case ".mp3":
                                resp.ContentType = "audio/mpeg";
                                break;
                            case ".gif":
                                resp.ContentType = "image/gif";
                                break;
                            case ".jpeg":
                            case ".jpg":
                                resp.ContentType = "image/jpeg";
                                break;
                            case ".png":
                                resp.ContentType = "image/png";
                                break;
                            case ".tif":
                            case ".tiff":
                                resp.ContentType = "image/tiff";
                                break;
                            case ".ico":
                                resp.ContentType = "image/vnd.microsoft.icon";
                                break;
                            case ".css":
                                resp.ContentType = "text/css";
                                break;
                            case ".html":
                            case ".htm":
                                resp.ContentType = "text/html";
                                break;
                            case ".txt":
                            case ".log":
                                resp.ContentType = "text/plain";
                                break;
                            case ".xml":
                                resp.ContentType = "text/xml";
                                break;
                            case ".mp4":
                                resp.ContentType = "video/mp4";
                                break;
                            default:
                                resp.ContentType = "application/octet-stream";
                                break;
                        }
                        byte[] file = File.ReadAllBytes(pageDirectory + path);
                        resp.ContentEncoding = Encoding.UTF8;
                        resp.ContentLength64 = file.LongLength;

                        await resp.OutputStream.WriteAsync(file, 0, file.Length);
                        resp.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.Red, "[Error]", ConsoleColor.White, e.Message);
                        if(e is FileNotFoundException || e is DirectoryNotFoundException)
                        {
                            resp.ContentType = "text/html";
                            byte[] data = Encoding.UTF8.GetBytes("<h1>HTTP Error 404 - File Not Found</h1>");
                            resp.ContentEncoding = Encoding.UTF8;
                            resp.ContentLength64 = data.LongLength;

                            await resp.OutputStream.WriteAsync(data, 0, data.Length);
                            resp.Close();
                        }
                    }

                }
            }
        }
        Task listenTask;

        public void Start(int Port = 80)
        {
            listener.Prefixes.Add($"http://*:{Port}/");
            new Task(() =>{
                try
                {
                    listener.Start();
                    IsReady = true;
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, $" Listening for connections on port: {Port}");

                    // Handle requests
                    listenTask = HandleIncomingConnections();
                    //listenTask.Start();
                    listenTask.GetAwaiter().GetResult();

                    // Close the listener
                    listener.Close();
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.Red, "[Error] ", ConsoleColor.White, $"[{e.GetType()}] {e.Message}");
                    Error(e.Message);
                }
            }).Start();
        }
        public void Stop()
        {
            Logger.Log(ConsoleColor.DarkMagenta, "[HTTP Server]", ConsoleColor.White, " Shuttingdown");
            runServer = false;
            while (IsRunning) ;
            //if(listener.IsListening)
            //  listener.Close();
        }
        public bool IsRunning { get => runServer; }
    }
    static class POST_File
    {
        static string GetBoundary(string ctype)
        {
            return "--" + ctype.Split(';')[1].Split('=')[1];
        }
        public static void SaveFile(string path, Stream input, Encoding enc, string ctype)
        {
            byte[] boundaryBytes = enc.GetBytes(GetBoundary(ctype));
            int boundaryLen = boundaryBytes.Length;

            using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[1024];
                int len = input.Read(buffer, 0, 1024);
                int startPos = -1;

                // Find start boundary
                while (true)
                {
                    if (len == 0)
                    {
                        throw new Exception("Start Boundaray Not Found");
                    }

                    startPos = IndexOf(buffer, len, boundaryBytes);
                    if (startPos >= 0)
                    {
                        break;
                    }
                    else
                    {
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen);
                    }
                }

                // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                for (int i = 0; i < 4; i++)
                {
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new Exception("Preamble not Found.");
                        }

                        startPos = Array.IndexOf(buffer, enc.GetBytes("\n")[0], startPos);
                        if (startPos >= 0)
                        {
                            startPos++;
                            break;
                        }
                        else
                        {
                            len = input.Read(buffer, 0, 1024);
                        }
                    }
                }

                Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                len = len - startPos;

                while (true)
                {
                    int endPos = IndexOf(buffer, len, boundaryBytes);
                    if (endPos >= 0)
                    {
                        if (endPos > 0) output.Write(buffer, 0, endPos - 2);
                        break;
                    }
                    else if (len <= boundaryLen)
                    {
                        throw new Exception("End Boundaray Not Found");
                    }
                    else
                    {
                        output.Write(buffer, 0, len - boundaryLen);
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                    }
                }
            }
        }
        static int IndexOf(byte[] buffer, int len, byte[] boundaryBytes)
        {
            for (int i = 0; i <= len - boundaryBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
