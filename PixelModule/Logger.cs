using System;
using System.IO;

namespace SharpClock
{
    public static class Logger
    {
        static readonly string LogDir = Environment.OSVersion.Platform == PlatformID.Unix
            ? "/var/log/sharpclock"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "sharpclock");

        public static string LogFile { get; } = Path.Combine(LogDir, "sharpclock.log");

        static DateTime currentDay = DateTime.Today;

        static Logger()
        {
            Directory.CreateDirectory(LogDir);
            // Rotate on startup if existing log is from a previous day
            if (File.Exists(LogFile) && File.GetLastWriteTime(LogFile).Date < DateTime.Today)
                Rotate(File.GetLastWriteTime(LogFile).Date);
        }

        static void Rotate(DateTime day)
        {
            string archive = Path.Combine(LogDir, $"sharpclock-{day:yyyy-MM-dd}.log");
            if (!File.Exists(archive))
                File.Move(LogFile, archive);
            else
                File.Delete(LogFile);

            var cutoff = DateTime.Today.AddDays(-14);
            foreach (var f in Directory.GetFiles(LogDir, "sharpclock-*.log"))
                if (File.GetLastWriteTime(f).Date < cutoff)
                    File.Delete(f);
        }

        public static void Clear()
        {
            File.Delete(LogFile);
        }

        public static void Log(params object[] args)
        {
            string logString = "";
            foreach (var arg in args)
            {
                if (arg is ConsoleColor)
                    Console.ForegroundColor = (ConsoleColor)arg;
                else
                {
                    Console.Write(arg.ToString());
                    logString += arg.ToString();
                }
            }
            Console.ResetColor();
            Console.WriteLine();

            var today = DateTime.Today;
            if (today != currentDay)
            {
                Rotate(currentDay);
                currentDay = today;
            }

            using (var sw = File.AppendText(LogFile))
                sw.WriteLine(logString);
        }
    }
}
