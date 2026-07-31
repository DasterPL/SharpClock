using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace SharpClock
{
    /// <summary>
    /// Logger that never blocks its caller. Callers only enqueue; a single background thread
    /// owns the console and the log file. This matters because the SD card in this device can
    /// stall for minutes at a time (mmc_claim_host), and the render loop must keep drawing
    /// instead of parking in uninterruptible I/O wait.
    /// </summary>
    class FileLogger : ILogger
    {
        static readonly string LogDir = Environment.OSVersion.Platform == PlatformID.Unix
            ? "/var/log/sharpclock"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "sharpclock");

        public string LogFile { get; } = Path.Combine(LogDir, "sharpclock.log");

        const int QueueCapacity = 512;

        readonly BlockingCollection<object[]> _queue =
            new BlockingCollection<object[]>(new ConcurrentQueue<object[]>(), QueueCapacity);

        readonly Thread _writer;
        DateTime _currentDay = DateTime.Today;
        StreamWriter _sw;
        int _dropped;

        public FileLogger()
        {
            Directory.CreateDirectory(LogDir);
            if (File.Exists(LogFile) && File.GetLastWriteTime(LogFile).Date < DateTime.Today)
                Rotate(File.GetLastWriteTime(LogFile).Date);

            _writer = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "log-writer",
                Priority = ThreadPriority.BelowNormal,
            };
            _writer.Start();
        }

        // ── Producer side (any thread, never blocks, never throws) ───────

        public void Log(params object[] args)
        {
            if (args == null || args.Length == 0) args = new object[] { "" };
            if (!_queue.TryAdd(args))
                Interlocked.Increment(ref _dropped);
        }

        /// <summary>Best-effort drain, used on shutdown so the last lines survive.</summary>
        public void Flush(int timeoutMs = 1500)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (_queue.Count > 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(10);
        }

        // ── Consumer side (single thread owns console + file) ────────────

        void WriterLoop()
        {
            foreach (var args in _queue.GetConsumingEnumerable())
            {
                try { WriteOne(args); }
                catch { /* a broken console or a stalled card must not kill the logger */ }
            }
        }

        void WriteOne(object[] args)
        {
            var line = new StringBuilder();
            foreach (var arg in args)
            {
                if (arg is ConsoleColor color)
                {
                    try { Console.ForegroundColor = color; } catch { }
                }
                else
                {
                    string s = arg?.ToString() ?? "";
                    try { Console.Write(s); } catch { }
                    line.Append(s);
                }
            }
            try { Console.ResetColor(); Console.WriteLine(); } catch { }

            var today = DateTime.Today;
            if (today != _currentDay)
            {
                CloseFile();
                Rotate(_currentDay);
                _currentDay = today;
            }

            int dropped = Interlocked.Exchange(ref _dropped, 0);
            if (dropped > 0)
                AppendLine($"[Logger] {dropped} lines dropped (log queue full)");

            AppendLine(line.ToString());
        }

        void AppendLine(string text)
        {
            try
            {
                if (_sw == null)
                {
                    var fs = new FileStream(LogFile, FileMode.Append, FileAccess.Write,
                                            FileShare.ReadWrite, 4096);
                    _sw = new StreamWriter(fs) { AutoFlush = true };
                }
                _sw.WriteLine(text);
            }
            catch
            {
                CloseFile(); // retry on the next line with a fresh handle
            }
        }

        void CloseFile()
        {
            try { _sw?.Dispose(); } catch { }
            _sw = null;
        }

        void Rotate(DateTime day)
        {
            try
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
            catch { }
        }

        // ── Reading ─────────────────────────────────────────────────────

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
