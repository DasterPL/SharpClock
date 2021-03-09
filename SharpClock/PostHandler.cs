using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SharpClock
{
    class PostHandler
    {
        HttpServer WebServer;
        public PostHandler(HttpServer WebServer)
        {
            this.WebServer = WebServer;
            WebServer.OnError += WebServer_OnError;
            WebServer.OnPost += WebServer_OnPost;
        }

        private void WebServer_OnError(string error)
        {
            //Program.Kill();
            //Environment.Exit(1);
        }

        private void WebServer_OnPost(string path, System.Collections.Specialized.NameValueCollection query)
        {
            var Config = new Config();
            switch (path)
            {
                case "/shutdown":
                    Program.Kill();
                    WebServer.PostResponse = "\"Shutingdown\"";
                    if (query["hard"] == "true")
                        Process.Start("/bin/bash", "-c \"shutdown 0\"");
                    else if (query["hard"] == "reboot")
                        Process.Start("/bin/bash", "-c \"reboot\"");
                    else if (query["hard"] == "restart")
                        throw new NotImplementedException();
                    Environment.Exit(0);
                    break;
                case "/getModules":
                    {
                        JArray json = new JArray();
                        var modules = PixelRenderer.Pixel.GetModules;
                        foreach (var module in modules)
                        {
                            dynamic m = new JObject();
                            m.Name = module.Name;
                            m.Status = module.IsRunning == true ? "started" : "stoped";
                            m.Icon = module.Icon;
                            JArray v = new JArray();
                            foreach (var prop in module.GetType().GetProperties())
                            {
                                dynamic p = new JObject();
                                p.Name = prop.Name;
                                p.VisibleName = new JArray();
                                foreach (VisibleName visibleName in prop.GetCustomAttributes(typeof(VisibleName), true))
                                {
                                    dynamic vn = new JObject();
                                    vn.lang = visibleName.lang;
                                    vn.value = visibleName.value;

                                    p.VisibleName.Add(vn);
                                }
                                if (prop.Name == "Name" || prop.Name == "IsRunning" || prop.Name == "Icon")
                                {
                                    continue;
                                }
                                else if (prop.PropertyType.BaseType == typeof(Enum))
                                {
                                    p.Value = prop.GetValue(module).ToString();
                                    p.type = "Enum";
                                    p.options = string.Join(",", Enum.GetNames(prop.PropertyType));
                                    p.visibleOptions = new JArray();
                                    foreach (VisibleNameEnum visibleName in prop.PropertyType.GetCustomAttributes(typeof(VisibleNameEnum), true))
                                    {
                                        dynamic vn = new JObject();
                                        vn.lang = visibleName.lang;

                                        dynamic vns = new JArray();
                                        foreach (var item in visibleName.values)
                                        {
                                            vns.Add(item);
                                        }
                                        vn.values = vns;
                                        p.visibleOptions.Add(vn);
                                    }
                                }
                                else if (prop.PropertyType == typeof(Color))
                                {
                                    var c = (Color)prop.GetValue(module);
                                    p.Value = $"#{c.R.ToString("X2")}{c.G.ToString("X2")}{c.B.ToString("X2")}";
                                    p.type = "Color";
                                }
                                else
                                {
                                    p.Value = prop.GetValue(module).ToString();
                                    p.type = prop.PropertyType.Name;
                                }
                                v.Add(p);
                            }
                            m.Values = v;
                            json.Add(m);
                        }
                        WebServer.PostResponse = json;//.ToString();
                    }
                    break;
                case "/modifyModuleSettings":
                    {
                        var module = PixelRenderer.Pixel.GetModule(query["Module"]);
                        query.Remove("Module");

                        if (bool.Parse(query["Power"]) && !module.IsRunning)
                            module.Start(Program.UpTime);
                        else if (!bool.Parse(query["Power"]) && module.IsRunning)
                            module.Stop();
                        query.Remove("Power");

                        module.Timer = int.Parse(query["Timer"]);
                        query.Remove("Timer");

                        module.Visible = bool.Parse(query["Visible"]);
                        query.Remove("Wisible");

                        var type = module.GetType();
                        foreach (var param in query.AllKeys)
                        {
                            var prop = type.GetProperty(param);
                            if (prop.PropertyType == typeof(int))
                                prop.SetValue(module, int.Parse(query[param]));
                            else if (prop.PropertyType == typeof(bool))
                                prop.SetValue(module, bool.Parse(query[param]));
                            else if (prop.PropertyType == typeof(string))
                                prop.SetValue(module, query[param]);
                            else if (prop.PropertyType == typeof(Color))
                                prop.SetValue(module, ColorTranslator.FromHtml(query[param]));
                            else if (prop.PropertyType.BaseType == typeof(Enum))
                                prop.SetValue(module, Enum.Parse(prop.PropertyType, query[param]));
                            else if (prop.PropertyType == typeof(TimeSpan))
                            {
                                var time = Array.ConvertAll(query[param].Split(':'), int.Parse);
                                prop.SetValue(module, new TimeSpan(time[0], time[1], 0));
                            }
                            else
                                Logger.Log(ConsoleColor.Blue, $"[{module.Name}]:", ConsoleColor.Red, $"Unregistered Type! - {prop.PropertyType.Name}");
                            Logger.Log(ConsoleColor.DarkMagenta, "Setting: ", ConsoleColor.White, $"{prop.Name} = {prop.GetValue(module)}, Type: {prop.PropertyType.Name}");
                        }
                        Config.EditModules(PixelRenderer.Pixel.GetModules);

                        dynamic JmodifyModule = new JObject();
                        JmodifyModule.Name = module.Name;
                        JmodifyModule.Status = module.IsRunning;
                        WebServer.PostResponse = JmodifyModule;//.ToString();
                        break;
                    }
                case "/sortModules":
                    //Logger.Log(query["order[]"]);
                    Config.SortModules(query["order[]"].Split(','));
                    PixelRenderer.Pixel.Reload();
                    break;
                case "/nextModule":
                    PixelRenderer.Pixel.NextModule();
                    WebServer.PostResponse = "\"" + PixelRenderer.Pixel.Current.Name + "\"";
                    break;
                case "/reloadModule":
                    PixelRenderer.Pixel.GetModule(query["name"]).Reload();
                    WebServer.PostResponse = "true";
                    break;
                case "/pause":
                    PixelRenderer.Pixel.Pause = !PixelRenderer.Pixel.Pause;
                    dynamic Jpause = new JObject();
                    Jpause.Pause = PixelRenderer.Pixel.Pause;
                    WebServer.PostResponse = Jpause;//.ToString();
                    break;
                case "/switchModule":
                    dynamic Jswitch = new JObject();
                    Jswitch.Pause = PixelRenderer.Pixel.Pause;
                    if (PixelRenderer.Pixel.SwitchModule(PixelRenderer.Pixel.GetModule(query["name"]), bool.Parse(query["pause"])))
                        Jswitch.Error = false;
                    else
                        Jswitch.Error = true;
                    WebServer.PostResponse = Jswitch;//.ToString();
                    break;
                case "/Brightness":
                    var brightness = byte.Parse(query["Value"]);
                    PixelDraw.Screen.Brightness = brightness;
                    Config.Brightness = brightness;
                    WebServer.PostResponse = "true";
                    break;
                case "/getProperties":
                    dynamic Jprop = new JObject();
                    Jprop.Brightness = PixelDraw.Screen.Brightness;
                    Jprop.Pause = PixelRenderer.Pixel.Pause;
                    Jprop.AnimatedSwitching = PixelRenderer.Pixel.AnimatedSwitching;
                    WebServer.PostResponse = Jprop;
                    break;
                case "/AnimatedSwitching":
                    var SwitchValue = bool.Parse(query["Value"]);
                    PixelRenderer.Pixel.AnimatedSwitching = SwitchValue;
                    Config.AnimatedSwitching = SwitchValue;
                    WebServer.PostResponse = "true";
                    break;
                case "/getScreen":
                    using (Image image = PixelDraw.Screen.GetScreen())
                    using (MemoryStream m = new MemoryStream())
                    {
                        image.Save(m, image.RawFormat);
                        byte[] imageBytes = m.ToArray();

                        string base64String = Convert.ToBase64String(imageBytes);
                        dynamic Jscreen = new JObject();
                        Jscreen.Screeen = base64String;
                        WebServer.PostResponse = Jscreen;//.ToString();
                    }
                    break;
                case "/AppInstall":
                    string mName = AppDomain.CurrentDomain.BaseDirectory +"Modules/" + query["fileName"];
                    Console.WriteLine(mName);
                    WebServer.PostResponse = "Modules/" + query["fileName"];
                    new Task(async () =>
                    {
                        await Task.Delay(3000);
                        
                        var unixFileInfo = new Mono.Unix.UnixFileInfo(mName);
                        unixFileInfo.SetOwner(new Mono.Unix.UnixUserInfo("pi"));
                         
                        PixelRenderer.Pixel.LoadModule(mName);
                    }).Start();
                    break;
                case "/RemoveDll":
                    throw new NotImplementedException();
                    break;
                case "/GetDlls":
                    var modulesFiles = Directory.GetFiles("Modules/");
                    WebServer.PostResponse = JArray.FromObject(modulesFiles.Select(s => Path.GetFileName(s)).ToArray());
                    break;
            }
        }
    }
}
