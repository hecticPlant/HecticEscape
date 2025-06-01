using System;
using System.Collections.Generic;

namespace HecticEscape
{
    public class LanguageManager
    {
        private readonly MainWindowSection _mw;

        public LanguageManager(MainWindowSection mainWindowSection)
        {
            _mw = mainWindowSection ?? throw new ArgumentNullException(nameof(mainWindowSection));
        }

        /// <summary>
        /// Liefert den übersetzten Text für einen Key in der Form "Timer-Tab.Header"
        /// </summary>
        public string Get(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                return "[[Key fehlt]]";

            var parts = keyPath.Split('.');
            if (parts.Length != 2)
            {
                Logger.Instance.Log($"Ungültiger KeyPath: {keyPath}", LogLevel.Warn);
                return $"[[{keyPath}]]";
            }

            var sectionName = parts[0];  // z.B. "Timer-Tab" oder "WebsitesTab"
            var fieldName = parts[1];    // z.B. "Header" oder "ShowBlockedWebsitesButton"

            Dictionary<string, string>? dict = sectionName switch
            {
                "Timer-Tab" => _mw.TimerTab,
                "WebsitesTab" => _mw.WebsitesTab,
                "ProzesseTab" => _mw.ProzesseTab,
                "GruppenTab" => _mw.GruppenTab,
                "SteuerungTab" => _mw.SteuerungTab,
                "StatusBar" => _mw.StatusBar,
                "Overlay" => _mw.StatusBar,
                "ErrorMessages" => _mw.ErrorMessages,
                _ => null
            };

            if (dict == null)
            {
                Logger.Instance.Log($"Section '{sectionName}' nicht gefunden (KeyPath = {keyPath}).", LogLevel.Warn);
                return $"[[{keyPath}]]";
            }

            if (!dict.TryGetValue(fieldName, out var text))
            {
                Logger.Instance.Log($"Feld '{fieldName}' nicht gefunden in Section '{sectionName}' (KeyPath = {keyPath}).", LogLevel.Warn);
                return $"[[{keyPath}]]";
            }

            return text;
        }
    }
}
