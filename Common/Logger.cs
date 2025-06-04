using System;
using System.Collections.Generic;
using System.IO;

namespace REALIS.Common
{
    /// <summary>
    /// Simple thread-safe logger that writes to REALIS.log.
    /// Prevents spam by throttling repeated messages.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, DateTime> _lastMessageTime = new();
        private const int THROTTLE_MS = 1000; // avoid spam
        private static readonly string _logPath = string.Empty;

        static Logger()
        {
            try
            {
                _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "REALIS.log");
                File.AppendAllText(_logPath, $"--- Logging started {DateTime.Now:u} ---{Environment.NewLine}");
            }
            catch
            {
                // ignore errors
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            lock (_lock)
            {
                if (_lastMessageTime.TryGetValue(message, out var last) &&
                    (DateTime.Now - last).TotalMilliseconds < THROTTLE_MS)
                {
                    return;
                }
                _lastMessageTime[message] = DateTime.Now;
                try
                {
                    File.AppendAllText(_logPath, $"{DateTime.Now:u} [{level}] {message}{Environment.NewLine}");
                }
                catch
                {
                    // suppress file errors
                }
            }
        }
    }
}
