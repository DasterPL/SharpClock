using SharpClock;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Drawing;
using System.Reflection;

namespace FoxesCloud
{
    public class PixelFoxCloud : SharpClock.PixelModule
    {
        string token = null;
        dynamic json;
        int dataErrCount = 0;
        GifImage logo;
        bool wasVisible = false;
        public PixelFoxCloud()
        {
            Icon = "bolt";
            logo = new GifImage(Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream($"FoxesCloud.logo.gif")));
            Tickrate = 1000 * 60 * 10; //10 minutes update;
        }
        public override void Draw(Stopwatch stopwatch)
        {
            try
            {
                float todayGeneration = json.result.today.generation;
                float currentGeneration = json.result.power;

                Screen.SetImage(logo.GetCurrentFrame, 0);
                
                int w = Screen.SetText(todayGeneration.ToString("#.#"), Color.DarkGreen, 8, 0);
                Screen.SetText(currentGeneration.ToString("#.#"), Color.DarkRed, w+1, 0);
            }
            catch (Exception)
            {
                Screen.SetText("no data", Color.Blue, 0);
            }
        }

        protected override void Update(Stopwatch stopwatch)
        {
            if(DateTime.Now.Hour>21 || DateTime.Now.Hour < 6)
            {
                wasVisible = Visible == true; 
                Visible = false;
                return;
            }
            else if(wasVisible)
            {
                Visible = true;
            }

            if (dataErrCount > 5)
            {
                dataErrCount = 0;
                Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.Red, "[Error] ", ConsoleColor.White, "5 attemps have benn made, data has not been downloaded, stopping the module!");
                Stop();
            }
            if (token == null)
            {
                token = getToken("daster", "qsxd2hll");
            }
            string stationID = getDropList()[0];
            json = getData(stationID);
            if(json.errno != 0)
            {
                dataErrCount++;
                token = null;
                Update(stopwatch);
            }
        }
        JObject getData(string stationID)
        {
            string url = $"https://www.foxesscloud.com/c/v0/plant/earnings/detail?stationID={stationID}";
            using (WebClient wc = new WebClient())
            {
                try
                {
                    wc.Headers.Add("token", token);
                    string data = wc.DownloadString(url);
                    Console.WriteLine(data);
                    dataErrCount = 0;
                    return JObject.Parse(data);
                    //Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Loading weather Completed!");
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.Red, "[Error] ", ConsoleColor.White, e.Message);
                    JObject data = new JObject();
                    data.Add("errno", 1);
                    data.Add("result", null);
                    return data;
                }
            }
        }
        string[] getDropList()
        {
            string url = "https://www.foxesscloud.com/c/v0/plant/droplist";
            using (WebClient wc = new WebClient())
            {
                try
                {
                    wc.Headers.Add("token", token);
                    string data = wc.DownloadString(url);
                    Console.WriteLine(data);
                    JObject dataParsed = JObject.Parse(data);
                    if (dataParsed["errno"].Value<int>() == 0)
                    {
                        return dataParsed["result"]["plants"].Values<string>("stationID").ToArray();
                    }
                    else
                    {
                        return null;
                    }
                    //Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.White, " Loading weather Completed!");
                }
                catch (Exception e)
                {
                    Logger.Log(ConsoleColor.Blue, $"[{this.GetType().Name}]:", ConsoleColor.Red, "[Error] ", ConsoleColor.White, e.Message);
                    return null;
                }
            }
        }
        string getToken(string user, string password)
        {
            string url = "https://www.foxesscloud.com/c/v0/user/login";
            string param = $"user={user}&password={md5(password)}";
            try
            {
                using (WebClient wc = new WebClient())
                {
                    string result = wc.UploadString(url, "POST", param);
                    Console.WriteLine(result);
                    dynamic resultParsed = JObject.Parse(result);
                    if (resultParsed.errno == 0)
                    {
                        return resultParsed.result.token;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        string md5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString().ToLower();
            }
        }
    }
}
