using Newtonsoft.Json.Linq;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SharpClock
{
    class PostHandler
    {
        public PostHandler(HttpServer WebServer)
        {
            WebServer.OnError += _ => { };
            WebServer.OnRequest += HandleRequest;
        }

        static JObject Ok(object result = null)
        {
            dynamic j = new JObject();
            j.result = result != null ? JToken.FromObject(result) : JValue.CreateNull();
            return j;
        }

        static JObject Err(string message)
        {
            dynamic j = new JObject();
            j.error = message;
            return j;
        }

        JToken HandleRequest(string method, string path, NameValueCollection q)
        {
            // GET /services
            if (method == "GET" && path == "/services")
            {
                return new JArray(PixelService.All.Select(s =>
                {
                    dynamic j = new JObject();
                    j.Name      = s.Name;
                    j.IsRunning = s.IsRunning;
                    j.LastRun   = s.LastRun == DateTime.MinValue ? null : s.LastRun.ToString("o");
                    j.LastError = s.LastError;
                    return (JToken)j;
                }).ToArray());
            }

            // PATCH /services/{name}
            if (method == "PATCH" && path.StartsWith("/services/"))
            {
                string svcName = path.Substring("/services/".Length);
                var svc = PixelService.All.FirstOrDefault(s => s.Name == svcName);
                if (svc == null) return Err("Service not found");
                if (bool.TryParse(q["Power"], out bool power))
                {
                    if (power && !svc.IsRunning) svc.Start();
                    else if (!power && svc.IsRunning) svc.Stop();
                }
                dynamic r = new JObject();
                r.Name      = svc.Name;
                r.IsRunning = svc.IsRunning;
                r.LastRun   = svc.LastRun == DateTime.MinValue ? null : svc.LastRun.ToString("o");
                r.LastError = svc.LastError;
                return r;
            }

            // GET /modules
            if (method == "GET" && path == "/modules")
                return GetModules();

            // PATCH /modules/{name}
            if (method == "PATCH" && path.StartsWith("/modules/"))
                return ModifyModule(path.Substring("/modules/".Length), q);

            // PUT /modules/order
            if (method == "PUT" && path == "/modules/order")
            {
                Config.SortModules(q["order[]"].Split(','));
                PixelRenderer.Pixel.Reload();
                return Ok(true);
            }

            // POST /modules/next
            if (method == "POST" && path == "/modules/next")
            {
                PixelRenderer.Pixel.NextModule();
                return Ok(PixelRenderer.Pixel.Current.Name);
            }

            // POST /modules/switch
            if (method == "POST" && path == "/modules/switch")
            {
                bool error = !PixelRenderer.Pixel.SwitchModule(
                    PixelRenderer.Pixel.GetModule(q["name"]),
                    bool.TryParse(q["pause"], out bool sp) && sp);
                dynamic r = new JObject();
                r.Pause   = PixelRenderer.Pixel.Pause;
                r.Current = PixelRenderer.Pixel.Current?.Name;
                r.Error   = error;
                return r;
            }

            // POST /modules/reload
            if (method == "POST" && path == "/modules/reload")
            {
                PixelRenderer.Pixel.GetModule(q["name"]).Reload();
                return Ok(true);
            }

            // POST /modules/{name}/button
            if (method == "POST" && path.StartsWith("/modules/") && path.EndsWith("/button"))
            {
                string mName = path.Substring("/modules/".Length, path.Length - "/modules/".Length - "/button".Length);
                var mod = PixelRenderer.Pixel.GetModule(mName);
                if (mod == null) return Err("Module not found");
                if (!Enum.TryParse(q["id"], out ButtonId btn)) return Err("Invalid button id");
                mod.OnButtonClick(btn);
                return Ok(mod.GetState());
            }

            // POST /modules/pause
            if (method == "POST" && path == "/modules/pause")
            {
                PixelRenderer.Pixel.Pause = !PixelRenderer.Pixel.Pause;
                dynamic r = new JObject();
                r.Pause = PixelRenderer.Pixel.Pause;
                return r;
            }

            // GET /globalSettings
            if (method == "GET" && path == "/globalSettings")
                return BuildGlobalSettings();

            // PATCH /globalSettings/{name}
            if (method == "PATCH" && path.StartsWith("/globalSettings/"))
                return ModifyGlobalSettings(path.Substring("/globalSettings/".Length), q);

            // GET /properties
            if (method == "GET" && path == "/properties")
                return BuildProperties();

            // PATCH /properties
            if (method == "PATCH" && path == "/properties")
            {
                if (byte.TryParse(q["Brightness"], out byte br))
                {
                    PixelDraw.Screen.Brightness = br;
                    Config.Brightness = br;
                }
                if (bool.TryParse(q["AnimatedSwitching"], out bool anim))
                {
                    PixelRenderer.Pixel.AnimatedSwitching = anim;
                    Config.AnimatedSwitching = anim;
                }
                if (bool.TryParse(q["Pause"], out bool pause))
                    PixelRenderer.Pixel.Pause = pause;
                return BuildProperties();
            }

            // GET /screen
            if (method == "GET" && path == "/screen")
            {
                using (Image image = PixelDraw.Screen.GetScreen())
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, image.RawFormat);
                    dynamic r = new JObject();
                    r.Screen = Convert.ToBase64String(ms.ToArray());
                    return r;
                }
            }

            // GET /log
            if (method == "GET" && path == "/log")
            {
                string logContent = Program.AppLogger.GetLog(300);
                if (string.IsNullOrEmpty(logContent))
                    return JArray.FromObject(new string[0]);
                var lines = logContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                return JArray.FromObject(lines);
            }

            // GET /plugins
            if (method == "GET" && path == "/plugins")
                return JArray.FromObject(
                    PixelRenderer.Pixel.GetModules
                        .Select(m => Path.GetFileName(m.GetType().Assembly.Location))
                        .Distinct()
                        .ToArray());

            // POST /plugins  (file upload — fileName injected by HttpServer multipart parser)
            if (method == "POST" && path == "/plugins")
            {
                string dllName = q["fileName"];
                string mPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", dllName);
                new Task(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        PixelRenderer.Pixel.UnloadModule(dllName);
                        var ufi = new Mono.Unix.UnixFileInfo(mPath);
                        ufi.SetOwner(new Mono.Unix.UnixUserInfo("pi"));
                        PixelRenderer.Pixel.LoadModule(mPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ConsoleColor.Blue, "[AppInstall]:", ConsoleColor.Red, $"[Error] {ex.Message}");
                    }
                }).Start();
                return Ok("Modules/" + dllName);
            }

            // DELETE /plugins/{name}
            if (method == "DELETE" && path.StartsWith("/plugins/"))
            {
                string dllName = path.Substring("/plugins/".Length);
                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", dllName);
                if (!File.Exists(dllPath))
                    return Err("File not found");
                PixelRenderer.Pixel.UnloadModule(dllName);
                File.Delete(dllPath);
                return Ok(dllName);
            }

            // POST /system/shutdown
            if (method == "POST" && path == "/system/shutdown")
            {
                bool restart = q["hard"] == "restart";
                Program.Kill();
                Environment.Exit(restart ? 1 : 0);
                return Ok(restart ? "restarting" : "shutting_down");
            }

            // GET /wifi
            if (method == "GET" && path == "/wifi")
                return GetWifiStatus();

            // GET /wifi/scan
            if (method == "GET" && path == "/wifi/scan")
                return ScanWifi();

            // POST /wifi/connect
            if (method == "POST" && path == "/wifi/connect")
                return ConnectWifi(q["ssid"], q["password"]);

            return null;
        }

        static string RunCmd(string file, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                });
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return output;
            }
            catch { return ""; }
        }

        static JObject GetWifiStatus()
        {
            bool hotspot = RunCmd("systemctl", "is-active hostapd").Trim() == "active";
            string ipOutput = RunCmd("ip", "addr show wlan0");
            var ipMatch = System.Text.RegularExpressions.Regex.Match(ipOutput, @"inet (\d+\.\d+\.\d+\.\d+)");
            string ip = ipMatch.Success ? ipMatch.Groups[1].Value : "";
            string ssid = "";
            if (!hotspot)
            {
                string wpaStatus = RunCmd("wpa_cli", "-i wlan0 status");
                var ssidMatch = System.Text.RegularExpressions.Regex.Match(wpaStatus, @"^ssid=(.+)$", System.Text.RegularExpressions.RegexOptions.Multiline);
                ssid = ssidMatch.Success ? ssidMatch.Groups[1].Value.Trim() : "";
            }
            dynamic r = new JObject();
            r.mode = hotspot ? "hotspot" : "client";
            r.ssid = ssid;
            r.ip = ip;
            return r;
        }

        static JArray ScanWifi()
        {
            RunCmd("wpa_cli", "-i wlan0 scan");
            System.Threading.Thread.Sleep(2500);
            string results = RunCmd("wpa_cli", "-i wlan0 scan_results");
            var networks = new JArray();
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var line in results.Split('\n').Skip(1))
            {
                var parts = line.Split('\t');
                if (parts.Length < 5) continue;
                string ssid = parts[4].Trim();
                if (string.IsNullOrEmpty(ssid) || !seen.Add(ssid)) continue;
                int signal = int.TryParse(parts[2], out int sig) ? sig : -100;
                bool open = !parts[3].Contains("WPA") && !parts[3].Contains("WEP");
                dynamic n = new JObject();
                n.ssid = ssid;
                n.signal = signal;
                n.open = open;
                networks.Add(n);
            }
            return new JArray(networks.OrderByDescending(n => (int)n["signal"]));
        }

        static JObject ConnectWifi(string ssid, string password)
        {
            if (string.IsNullOrEmpty(ssid))
                return Err("SSID required");

            // Escape for wpa_supplicant.conf
            string eSsid = ssid.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string conf;
            if (string.IsNullOrEmpty(password))
                conf = $"ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev\nupdate_config=1\ncountry=PL\n\nnetwork={{\n\tssid=\"{eSsid}\"\n\tkey_mgmt=NONE\n}}\n";
            else
            {
                string ePsk = password.Replace("\\", "\\\\").Replace("\"", "\\\"");
                conf = $"ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev\nupdate_config=1\ncountry=PL\n\nnetwork={{\n\tssid=\"{eSsid}\"\n\tpsk=\"{ePsk}\"\n}}\n";
            }
            File.WriteAllText("/etc/wpa_supplicant/wpa_supplicant.conf", conf);

            new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(800);
                Process.Start("/bin/bash", "-c \"/usr/local/bin/wifi-to-client\"");
            }).Start();

            return Ok("connecting");
        }

        static JObject BuildProperties()
        {
            dynamic r = new JObject();
            r.Brightness = PixelDraw.Screen.Brightness;
            r.Pause = PixelRenderer.Pixel.Pause;
            r.Current = PixelRenderer.Pixel.Current?.Name;
            r.AnimatedSwitching = PixelRenderer.Pixel.AnimatedSwitching;
            return r;
        }

        static JArray GetModules()
        {
            JArray json = new JArray();
            foreach (var module in PixelRenderer.Pixel.GetModules)
            {
                dynamic m = new JObject();
                m.Name             = module.Name;
                m.Status           = module.IsRunning ? "started" : "stopped";
                m.Icon             = module.Icon;
                m.Dll              = Path.GetFileNameWithoutExtension(module.GetType().Assembly.Location);
                m.ExcludeFromQueue = module.ExcludeFromQueue;
                JArray v = new JArray();
                foreach (var entry in module.Settings.All)
                {
                    if (entry.Visibility != null && !entry.Visibility())
                        continue;

                    dynamic p = new JObject();
                    p.Name = entry.Key;
                    p.VisibleName = new JArray();
                    foreach (var kvp in entry.Labels)
                    {
                        dynamic vnObj = new JObject();
                        vnObj.lang = kvp.Key;
                        vnObj.value = kvp.Value;
                        p.VisibleName.Add(vnObj);
                    }

                    if (entry.Min.HasValue) p.min = entry.Min.Value;
                    if (entry.Max.HasValue) p.max = entry.Max.Value;
                    if (entry.Step.HasValue) p.step = entry.Step.Value;
                    if (entry.IsMultiline) p.multiline = true;

                    if (entry.ValueType.IsEnum)
                    {
                        p.Value = entry.Get().ToString();
                        p.type = "Enum";
                        p.options = string.Join(",", Enum.GetNames(entry.ValueType));
                        p.visibleOptions = new JArray();
                        foreach (var kvp in entry.EnumLabels)
                        {
                            dynamic vneObj = new JObject();
                            vneObj.lang = kvp.Key;
                            vneObj.values = new JArray(kvp.Value.Cast<object>().ToArray());
                            p.visibleOptions.Add(vneObj);
                        }
                    }
                    else if (entry.ValueType == typeof(Color))
                    {
                        var c = (Color)entry.Get();
                        p.Value = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                        p.type = "Color";
                    }
                    else if (entry.IsPassword)
                    {
                        p.Value = "";
                        p.type = "Password";
                    }
                    else if (entry.ValueType == typeof(float))
                    {
                        p.Value = ((float)entry.Get()).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        p.type = "Single";
                    }
                    else
                    {
                        p.Value = entry.Get().ToString();
                        p.type = entry.ValueType.Name;
                    }
                    v.Add(p);
                }
                m.Values = v;
                var state = module.GetState();
                if (state != null)
                {
                    dynamic extra = new JObject();
                    foreach (var kv in state)
                        extra[kv.Key] = kv.Value != null ? JToken.FromObject(kv.Value) : JValue.CreateNull();
                    m.Extra = extra;
                }
                json.Add(m);
            }
            return json;
        }

        static JArray BuildGlobalSettings()
        {
            var json = new JArray();
            foreach (var gs in PixelGlobalSettings.All)
            {
                dynamic g = new JObject();
                g.Name = gs.Name;
                var entries = new JArray();
                foreach (var entry in gs.Settings.All)
                {
                    if (entry.Visibility != null && !entry.Visibility()) continue;
                    dynamic p = new JObject();
                    p.Name = entry.Key;
                    p.VisibleName = new JArray();
                    foreach (var kvp in entry.Labels)
                    {
                        dynamic vnObj = new JObject();
                        vnObj.lang  = kvp.Key;
                        vnObj.value = kvp.Value;
                        p.VisibleName.Add(vnObj);
                    }
                    if (entry.Min.HasValue) p.min = entry.Min.Value;
                    if (entry.Max.HasValue) p.max = entry.Max.Value;
                    if (entry.Step.HasValue) p.step = entry.Step.Value;
                    if (entry.IsMultiline) p.multiline = true;
                    if (entry.IsPassword)
                    {
                        p.Value = "";
                        p.type  = "Password";
                    }
                    else if (entry.ValueType == typeof(System.Drawing.Color))
                    {
                        var c = (System.Drawing.Color)entry.Get();
                        p.Value = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                        p.type  = "Color";
                    }
                    else if (entry.ValueType == typeof(float))
                    {
                        p.Value = ((float)entry.Get()).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        p.type  = "Single";
                    }
                    else if (entry.ValueType.IsEnum)
                    {
                        p.Value = entry.Get().ToString();
                        p.type  = "Enum";
                        p.options = string.Join(",", Enum.GetNames(entry.ValueType));
                    }
                    else
                    {
                        p.Value = entry.Get().ToString();
                        p.type  = entry.ValueType.Name;
                    }
                    entries.Add(p);
                }
                g.Values = entries;
                json.Add(g);
            }
            return json;
        }

        static JObject ModifyGlobalSettings(string name, NameValueCollection q)
        {
            var gs = PixelGlobalSettings.All.FirstOrDefault(g => g.Name == name);
            if (gs == null) return Err("GlobalSettings not found");

            foreach (var entry in gs.Settings.All)
            {
                string val = q[entry.Key];
                if (val == null) continue;
                try
                {
                    entry.Set(val);
                    Logger.Log(ConsoleColor.DarkMagenta, "GlobalSetting: ", ConsoleColor.White,
                        $"{entry.Key} = {entry.Get()}, Type: {entry.ValueType.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Log(ConsoleColor.Blue, $"[{gs.Name}]:", ConsoleColor.Red, $"Failed to set {entry.Key}: {ex.Message}");
                }
            }
            Config.EditGlobalSettings(gs);
            return Ok(gs.Name);
        }

        static JObject ModifyModule(string name, NameValueCollection q)
        {
            var module = PixelRenderer.Pixel.GetModule(name);
            if (module == null)
                return Err("Module not found");

            if (bool.TryParse(q["Power"], out bool power))
            {
                if (power && !module.IsRunning) module.Start(Program.UpTime);
                else if (!power && module.IsRunning) module.Stop();
            }

            if (bool.TryParse(q["ExcludeFromQueue"], out bool exq))
                module.ExcludeFromQueue = exq;

            foreach (var entry in module.Settings.All)
            {
                string val = q[entry.Key];
                if (val == null) continue;
                try
                {
                    entry.Set(val);
                    Logger.Log(ConsoleColor.DarkMagenta, "Setting: ", ConsoleColor.White,
                        $"{entry.Key} = {entry.Get()}, Type: {entry.ValueType.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Log(ConsoleColor.Blue, $"[{module.Name}]:", ConsoleColor.Red, $"Failed to set {entry.Key}: {ex.Message}");
                }
            }
            Config.EditModule(module);

            dynamic result = new JObject();
            result.Name = module.Name;
            result.Status = module.IsRunning;
            return result;
        }
    }
}
