using SKAuto.Core.Interfaces;

namespace SKAuto.Core.Services
{
    public class LoggingService : ILoggingService
    {
        private readonly string _logFilePath;
        private readonly object _lock = new();

        public LoggingService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appData, "SKAuto", "Logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"{DateTime.Now:yyyyMMdd}.log");
        }

        public void LogInfo(string message) => WriteLog("INFO", message);
        public void LogWarning(string message) => WriteLog("WARN", message);
        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = message + (ex != null ? $"\nException: {ex}" : "");
            WriteLog("ERROR", fullMessage);
        }

        private void WriteLog(string level, string message)
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n");
            }
        }
    }
}