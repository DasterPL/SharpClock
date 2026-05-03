using System;
using System.IO;
using System.Windows.Media;

namespace SharpClock
{
    class FileLogger : ILogger
    {
        static readonly string LogDir = Environment.OSVersion.Platform == PlatformID.Unix
            ? "/var/log/sharpclock"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "sharpclock");

        public string LogFile { get; } = Path.Combine(LogDir, "sharpclock.log");

        DateTime _currentDay = DateTime.Today;

        public FileLogger()
        {
            Directory.CreateDirectory(LogDir);
            if (File.Exists(LogFile) && File.GetLastWriteTime(LogFile).Date < DateTime.Today)
                Rotate(File.GetLastWriteTime(LogFile).Date);
        }

        void Rotate(DateTime day)
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

        public void Log(params object[] args)
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
            if (today != _currentDay)
            {
                Rotate(_currentDay);
                _currentDay = today;
            }

            using (var sw = File.AppendText(LogFile))
                sw.WriteLine(logString);
        }
        public string GetLog(int lines = -1)
        {
            if (!File.Exists(LogFile))
                return "";
            if (lines == -1)
                return File.ReadAllText(LogFile);

            using (var fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long pos = fs.Length;
                int found = 0;
                var buf = new byte[4096];
                while (pos > 0 && found < lines)
                {
                    int chunk = (int)Math.Min(buf.Length, pos);
                    pos -= chunk;
                    fs.Seek(pos, SeekOrigin.Begin);
                    fs.Read(buf, 0, chunk);
                    for (int i = chunk - 1; i >= 0 && found < lines; i--)
                        if (buf[i] == '\n')
                            found++;
                    if (found < lines) continue;
                    // rewind to just after the last \n we counted
                    for (int i = chunk - 1; i >= 0; i--)
                        if (buf[i] == '\n') { pos += i + 1; break; }
                }
                fs.Seek(pos, SeekOrigin.Begin);
                using (var sr = new StreamReader(fs))
                    return sr.ReadToEnd();
            }
        }
    }
}
