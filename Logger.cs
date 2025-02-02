using System.IO;

namespace ScreenZen
{
    public class Logger
    {
        // Pfad der Logdatei. Sie können diesen Pfad auch über einen Konstruktorparameter konfigurieren.
        private readonly string logFilePath = "app.log";

        // Singleton-Instanz (optional, falls Sie einen globalen Logger möchten)
        private static Logger instance;
        public static Logger Instance => instance ?? (instance = new Logger());

        // Event, das Log-Nachrichten an Abonnenten sendet (z.B. an die UI)
        public event Action<string> LogMessageReceived;

        private Logger()
        {
            // Optional: Logdatei beim Start leeren oder einen Header schreiben.
            File.WriteAllText(logFilePath, $"Log gestartet: {DateTime.Now}{Environment.NewLine}");
        }

        // Hauptmethode, um Log-Nachrichten zu schreiben
        public void Log(string message)
        {
            string fullMessage = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} - {message}";

            // Schreibe in die Datei (asynchron möglich, wenn gewünscht)
            try
            {
                File.AppendAllText(logFilePath, fullMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Falls das Schreiben in die Logdatei fehlschlägt, können Sie hier reagieren
                // z.B. eine Exception loggen oder in Debug-Ausgaben schreiben.
            }

            // Lösen Sie das Event aus, damit Abonnenten (z.B. die UI) informiert werden.
            LogMessageReceived?.Invoke(fullMessage);
        }
    }
}
