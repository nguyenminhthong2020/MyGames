using System;
using System.IO;
using System.Text;

namespace MyGames.Desktop.Logs
{
    /// <summary>
    /// Simple file logger used across the app.
    /// Provides Information / Warning / Error signatures similar to Microsoft.Extensions.Logging style.
    /// </summary>
    public class LoggerService
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lock = new();

        public event Action<string>? LogAppended;

        public LoggerService()
        {
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(_logDirectory, $"log_{timestamp}.txt");
        }

        // Compatibility methods
        public void Information(string message, params object[] args) =>
            WriteLog("INFO", FormatMessage(message, args));

        public void Warning(string message, params object[] args) =>
            WriteLog("WARN", FormatMessage(message, args));

        public void Error(string message, params object[] args) =>
            WriteLog("ERROR", FormatMessage(message, args));

        // Overload that accepts an exception
        public void Error(Exception ex, string? message = null, params object[] args)
        {
            var prefix = string.IsNullOrEmpty(message) ? string.Empty : FormatMessage(message, args) + Environment.NewLine;
            var full = prefix + $"Exception: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            WriteLog("ERROR", full);
        }

        // Simple helper that existed before (kept for compatibility)
        public void Info(string message) => Information(message);
        public void Warn(string message) => Warning(message);
        public void Error(string message, Exception? ex = null)
        {
            if (ex is null) Error(message);
            else Error(ex, message);
        }

        private static string FormatMessage(string message, params object[] args)
        {
            return args != null && args.Length > 0 ? string.Format(message, args) : message;
        }

        private void WriteLog(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            lock (_lock)
            {
                try
                {
                    // Append with UTF8 to keep accents correct
                    File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {

                }
            }
            LogAppended?.Invoke(line);
        }
    }
}
