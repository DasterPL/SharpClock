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
            // GET /modules
            if (method == "GET" && path == "/modules")
                return GetModules();

            // PATCH /modules/{name}
            if (method == "PATCH" && path.StartsWith("/modules/"))
                return ModifyModule(path.Substring("/modules/".Length), q);

            // PUT /modules/order
            if (method == "PUT" && path == "/modules/order")
            {
                Config.Instance.SortModules(q["order[]"].Split(','));
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
                dynamic r = new JObject();
                r.Pause = PixelRenderer.Pixel.Pause;
                r.Error = !PixelRenderer.Pixel.SwitchModule(
                    PixelRenderer.Pixel.GetModule(q["name"]),
                    bool.TryParse(q["pause"], out bool sp) && sp);
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

            // GET /properties
            if (method == "GET" && path == "/properties")
                return BuildProperties();

            // PATCH /properties
            if (method == "PATCH" && path == "/properties")
            {
                if (byte.TryParse(q["Brightness"], out byte br))
                {
                    PixelDraw.Screen.Brightness = br;
                    Config.Instance.Brightness = br;
                }
                if (bool.TryParse(q["AnimatedSwitching"], out bool anim))
                {
                    PixelRenderer.Pixel.AnimatedSwitching = anim;
                    Config.Instance.AnimatedSwitching = anim;
                }
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
                string logFile = Program.AppLogger.LogFile;
                if (!File.Exists(logFile))
                    return JArray.FromObject(new string[0]);
                var lines = File.ReadAllLines(logFile);
                int skip = Math.Max(0, lines.Length - 300);
                return JArray.FromObject(lines.Skip(skip).ToArray());
            }

            // DELETE /log
            if (method == "DELETE" && path == "/log")
            {
                Program.AppLogger.Clear();
                return Ok(true);
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
                Program.Kill();
                if (q["hard"] == "true")
                    Process.Start("/bin/bash", "-c \"shutdown 0\"");
                else if (q["hard"] == "reboot")
                    Process.Start("/bin/bash", "-c \"reboot\"");
                else if (q["hard"] == "restart")
                    Process.Start("/bin/bash", "-c \"systemctl restart SharpClock\"");
                Environment.Exit(0);
                return Ok("shutting_down");
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
            r.AnimatedSwitching = PixelRenderer.Pixel.AnimatedSwitching;
            return r;
        }

        static JArray GetModules()
        {
            JArray json = new JArray();
            foreach (var module in PixelRenderer.Pixel.GetModules)
            {
                dynamic m = new JObject();
                m.Name = module.Name;
                m.Status = module.IsRunning ? "started" : "stopped";
                m.Icon = module.Icon;
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
            Config.Instance.EditModules(PixelRenderer.Pixel.GetModules);

            dynamic result = new JObject();
            result.Name = module.Name;
            result.Status = module.IsRunning;
            return result;
        }
    }
}
