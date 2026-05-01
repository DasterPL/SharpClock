namespace SharpClock
{
    public interface ILogger
    {
        string LogFile { get; }
        void Log(params object[] args);
        void Clear();
    }

    public static class Logger
    {
        static ILogger _impl;
        public static string LogFile => _impl?.LogFile;
        public static void SetImpl(ILogger impl) => _impl = impl;
        public static void Log(params object[] args) => _impl?.Log(args);
        public static void Clear() => _impl?.Clear();
    }
}
