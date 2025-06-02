using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HecticEscape
{
    public enum LogLevel
    {
        Info,
        Warn,
        Error,
        Debug,
        Verbose
    }

    public class Logger : IDisposable
    {
        private static readonly Lazy<Logger> _instance = new(() => new Logger());
        public static Logger Instance => _instance.Value;

        private readonly string logFilePath;
        private readonly BlockingCollection<string> logQueue = new();
        private readonly CancellationTokenSource cts = new();
        private readonly Task logTask;
        public bool IsDebugEnabled { get; set; } = false;
        public bool IsVerboseEnabled { get; set; } = false;

        private Logger()
        {
            // Logverzeichnis im Benutzerprofil (AppData\Roaming\HecticEscape\log)
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HecticEscape", "log");
            Directory.CreateDirectory(appDataPath);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(appDataPath, $"app_{timestamp}.log");

            logTask = Task.Run(ProcessLogQueue);
        }

        public void Log(
            string message,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (level == LogLevel.Debug && !IsDebugEnabled)
            {
                return;
            }

            if (level == LogLevel.Verbose && !IsVerboseEnabled)
            {
                return;
            }

            string callerInfo = $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMemberName}:{callerLineNumber}";
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{callerInfo}] {message}{Environment.NewLine}";
            logQueue.Add(logEntry);
        }

        public Task LogAsync(
            string message,
            LogLevel level = LogLevel.Info,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Log(message, level, callerMemberName, callerFilePath, callerLineNumber);
            return Task.CompletedTask;
        }

        private async Task ProcessLogQueue()
        {
            foreach (var logEntry in logQueue.GetConsumingEnumerable(cts.Token))
            {
                try
                {
                    await File.AppendAllTextAsync(logFilePath, logEntry);
                }
                catch
                {
                    // Fehler beim Schreiben ins Log ignorieren
                }
            }
        }

        public void Dispose()
        {
            cts.Cancel();
            logQueue.CompleteAdding();
            try { logTask.Wait(); } catch { }
            cts.Dispose();
            logQueue.Dispose();
        }
    }
}
