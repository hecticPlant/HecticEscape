using System.Diagnostics;
using System.IO;
using System;

namespace ScreenZen
{
    /// <summary>
    /// Diese Klasse verwaltet das Logging in eine .log Datei
    /// </summary>
    public class Logger
    {
        private readonly string logFilePath;

        private static Logger instance;
        public static Logger Instance => instance ?? (instance = new Logger());

        public event Action<string> LogMessageReceived;

        public Logger()
        {
            // Log-Datei dynamisch erstellen, basierend auf dem aktuellen Datum und der Uhrzeit
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = $"app_{timestamp}.log";

            // Die Log-Datei wird mit einem Header erstellt
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
                // Füge die Log-Nachricht in die Datei hinzu
                File.AppendAllText(logFilePath, fullMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Logge Fehler, wenn etwas schief geht
                Logger.Instance.Log($"Fehler: '{ex}'");
            }

            LogMessageReceived?.Invoke(fullMessage);
        }
    }
}
