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
        internal static ILogger _impl;
        public static void Log(params object[] args) => _impl?.Log(args);
    }
}
