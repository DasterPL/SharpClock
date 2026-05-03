using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace PixelFoxCloud
{
    class FoxEssData
    {
        public float PvPower;
        public float TodayYield;
    }

    class FoxEssClient
    {
        readonly string _apiKey;
        readonly string _deviceSn;

        public FoxEssClient(string apiKey, string deviceSn)
        {
            _apiKey = apiKey;
            _deviceSn = deviceSn;
        }

        public FoxEssData Fetch()
        {
            var data = new FoxEssData();
            data.PvPower = FetchRealTime();
            data.TodayYield = FetchTodayYield();
            return data;
        }

        float FetchRealTime()
        {
            const string path = "/op/v0/device/real/query";
            var body = new JObject
            {
                ["sn"] = _deviceSn,
                ["variables"] = new JArray("pvPower")
            };

            var result = Post(path, body);
            if (result == null) return 0f;

            var datas = result[0]?["datas"] as JArray ?? result;
            foreach (var item in datas)
                if ((string)item["variable"] == "pvPower")
                    return item["value"]?.Value<float>() ?? 0f;

            return 0f;
        }

        float FetchTodayYield()
        {
            const string path = "/op/v0/device/report/query";
            var now = DateTime.Now;
            var body = new JObject
            {
                ["sn"] = _deviceSn,
                ["dimension"] = "month",
                ["year"] = now.Year,
                ["month"] = now.Month,
                ["variables"] = new JArray("generation")
            };

            var result = Post(path, body);
            if (result == null) return 0f;

            var values = result[0]?["values"] as JArray;
            int dayIdx = now.Day - 1;
            if (values != null && dayIdx < values.Count)
                return values[dayIdx]?.Value<float>() ?? 0f;

            return 0f;
        }

        JArray Post(string path, JObject body)
        {
            string url = "https://www.foxesscloud.com" + path;
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = Md5(path + @"\r\n" + _apiKey + @"\r\n" + timestamp);

            using (var wc = new WebClient())
            {
                wc.Headers["token"] = _apiKey;
                wc.Headers["timestamp"] = timestamp;
                wc.Headers["signature"] = signature;
                wc.Headers["lang"] = "en";
                wc.Headers["Content-Type"] = "application/json";

                string response = wc.UploadString(url, "POST", body.ToString(Newtonsoft.Json.Formatting.None));
                var json = JObject.Parse(response);

                int errno = json["errno"]?.Value<int>() ?? -1;
                if (errno != 0)
                {
                    string msg = json["msg"]?.ToString() ?? json["message"]?.ToString() ?? "no message";
                    throw new Exception($"API error {errno}: {msg} [{path}]");
                }

                return json["result"] as JArray;
            }
        }

        static string Md5(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(32);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
