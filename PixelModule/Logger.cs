namespace SharpClock
{
    public interface ILogger
    {
        void Log(params object[] args);
        string GetLog(int lines = -1);
    }

    public static class Logger
    {
        internal static ILogger _impl;
        public static void Log(params object[] args) => _impl?.Log(args);
        public static string GetLog(int lines = -1) => _impl?.GetLog(lines) ?? "";
    }
}
