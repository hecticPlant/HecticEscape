using System.Diagnostics;
using System.IO;
using System.Windows;


namespace ScreenZen
{
 
    /// <summary>
    /// Diese Klasse verwaltet das Logging in eine .log Datei
    /// </summary>
    public class Logger
    {
        private readonly string logFilePath = "app.log";

        private static Logger instance;
        public static Logger Instance => instance ?? (instance = new Logger());

        public event Action<string> LogMessageReceived;

        public Logger()
        {
            File.WriteAllText(logFilePath, $"Log gestartet: {DateTime.Now}{Environment.NewLine}");
        }
    public void Log(string message)
        {
            // Hole die aktuelle Methode und Klasse
            var stackTrace = new StackTrace(true);
            var frame = stackTrace.GetFrame(1); // Der Frame 1 ist der Aufrufer der Methode
            string methodName = frame.GetMethod().Name;
            string className = frame.GetMethod().DeclaringType.Name;

            string fullMessage = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} - {className}.{methodName} - {message}";

            try
            {
                File.AppendAllText(logFilePath, fullMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler: '{ex}'");
            }

            LogMessageReceived?.Invoke(fullMessage);
        }
    }
}
