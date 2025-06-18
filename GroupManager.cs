using System.Collections.Generic;
using System.Linq;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet alle Gruppen-Operationen (CRUD, Aktiv-Status).
    /// Greift auf die Gruppen-Datenstruktur in ConfigReader zu.
    /// </summary>
    public class GroupManager : AManager
    {
        private readonly ConfigReader _configReader;

        public GroupManager(ConfigReader configReader) : base(configReader)
        {
            _configReader = configReader;
        }

        public override void Initialize()
        {
            Logger.Instance.Log("GroupManager initialisiert.", LogLevel.Info);
        }

        /// <summary>
        /// Gibt alle Gruppen zurück.
        /// </summary>
        public List<Gruppe> GetAllGroups()
        {
            Logger.Instance.Log("Alle Gruppen werden abgerufen.", LogLevel.Verbose);
            return _configReader.Config.Gruppen.Values.ToList();
        }

        /// <summary>
        /// Gibt die Gruppe mit dem angegebenen Namen zurück, oder null.
        /// </summary>
        public Gruppe? GetGroupByName(string groupName)
        {
            Logger.Instance.Log($"Suche Gruppe mit Namen: {groupName}", LogLevel.Verbose);
            _configReader.Config.Gruppen.TryGetValue(groupName, out var group);
            return group;
        }

        /// <summary>
        /// Erstellt eine neue Gruppe mit fortlaufender Nummer.
        /// </summary>
        public Gruppe CreateGroup()
        {
            int maxNum = _configReader.Config.Gruppen.Keys
                .Where(k => k.StartsWith("Gruppe "))
                .Select(k => int.TryParse(k.Split(' ').Last(), out int n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            string newName = $"Gruppe {maxNum + 1}";
            var newGroup = new Gruppe
            {
                Name = newName,
                Aktiv = true,
                Apps = new List<AppHE>(),
                Websites = new List<Website>()                
            };
            _configReader.Config.Gruppen.Add(newName, newGroup);
            Logger.Instance.Log($"Neue Gruppe erstellt: {newName}", LogLevel.Info);
            _configReader.SetSaveConfigFlag();
            return newGroup;
        }

        /// <summary>
        /// Löscht eine Gruppe.
        /// </summary>
        public bool DeleteGroup(Gruppe group)
        {
            Logger.Instance.Log($"Versuche Gruppe zu löschen: {group.Name}", LogLevel.Verbose);
            var key = _configReader.Config.Gruppen.FirstOrDefault(kvp => kvp.Value == group).Key;
            if (key != null)
            {
                _configReader.Config.Gruppen.Remove(key);
                return true;
            }
            _configReader.SetSaveConfigFlag();
            return false;
        }

        /// <summary>
        /// Setzt den Aktiv-Status einer Gruppe.
        /// </summary>
        public bool SetGroupActiveStatus(Gruppe group, bool isActive)
        {
            Logger.Instance.Log($"Setze Aktiv-Status für Gruppe '{group.Name}' auf {isActive}.", LogLevel.Verbose);
            if (group == null) return false;
            group.Aktiv = isActive;
            return true;
        }

        /// <summary>
        /// Gibt alle Gruppennamen zurück.
        /// </summary>
        public List<string> GetAllGroupNames()
        {
            Logger.Instance.Log("Alle Gruppennamen werden abgerufen.", LogLevel.Verbose);
            return _configReader.Config.Gruppen.Values.Where(g => !string.IsNullOrWhiteSpace(g.Name)).Select(g => g.Name).ToList();
        }

        /// <summary>
        /// Gibt alle aktiven Gruppen zurück.
        /// </summary>
        public List<Gruppe> GetAllActiveGroups()
        { 
            Logger.Instance.Log("Alle aktiven Gruppen werden abgerufen.", LogLevel.Verbose);
            return _configReader.Config.Gruppen.Values.Where(g => g.Aktiv).ToList();
        }

        public void SetDailyTimeMs(Gruppe group, long dailyTimeMs)
        {
            if (group == null) return;

            group.DailyTimeMs = dailyTimeMs;
            Logger.Instance.Log($"DailyTime von Gruppe {group.Name} auf {dailyTimeMs} gesetzts", LogLevel.Verbose);

            _configReader.SetSaveConfigFlag();
        }

        public long GetDailyTimeLeft(Gruppe group, DateOnly date)
        {
            Logger.Instance.Log($"Berechne verbleibende Zeit für Gruppe {group?.Name} am {date:yyyy-MM-dd}.", LogLevel.Verbose);
            if (group == null) return 0;
            var log = group.Logs?.FirstOrDefault(l => l.Date == date);
            long used = log?.TimeMs ?? 0;
            return group.DailyTimeMs - used;
        }

        public void SetTimeMS(Gruppe group, long timeMs)
        {
            if (group == null) return;
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);

            Log? log = group.Logs.FirstOrDefault(l => l.Date == date);
            if (log == null)
            {
                log = new Log { Date = date, TimeMs = 0 };
                group.Logs.Add(log);
                Logger.Instance.Log($"Neues Log für {group.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Warn);
            }
            else
            {
                log.TimeMs = timeMs;
                Logger.Instance.Log($"DailyTime von Gruppe {group.Name} auf {timeMs} gesetzts", LogLevel.Verbose);
            }
            _configReader.SetSaveConfigFlag();
        }
        
        public void AddTimeToLog(Gruppe gruppe, DateOnly date, long timeMs)
        {
            Logger.Instance.Log($"Füge {timeMs}ms zu Log von {gruppe.Name} am {date:yyyy-MM-dd} hinzu.", LogLevel.Verbose);
            if (gruppe == null || date == default || timeMs < 0)
            {
                Logger.Instance.Log("Ungültige Parameter für AddTimeToLog. Gruppe, Datum oder Zeit sind ungültig.", LogLevel.Error);
                return;
            }
            var log = gruppe.Logs.FirstOrDefault(l => l.Date == date);
            if (log == null)
            {
                log = new Log { Date = date, TimeMs = 0 };
                gruppe.Logs.Add(log);
                Logger.Instance.Log($"Neues Log für {gruppe.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Info);
            }
            log.TimeMs += timeMs;
            _configReader.SetSaveConfigFlag();
        }

        public List<Gruppe> GetGroupsWithLowDailyTimeLeft()
        {
            Logger.Instance.Log("Überprüfe, ob tägliche Zeit für Gruppen niedrig ist.", LogLevel.Verbose);
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            List<Gruppe> groups = GetAllActiveGroups();
            if (groups == null || groups.Count == 0)
            {
                Logger.Instance.Log("Keine aktiven Gruppen mit Apps gefunden.", LogLevel.Verbose);
                return new List<Gruppe>();
            }

            var lowTimeGroups = new List<Gruppe>();
            foreach (var group in groups)
            {
                long remaining = GetDailyTimeLeft(group, date);
                if (remaining < 65 * 60 * 1000)
                {
                    Logger.Instance.Log($"Gruppe {group.Name} zu lowTimeGroup hinzugefügt (restliche Zeit: {remaining} ms).", LogLevel.Verbose);
                    lowTimeGroups.Add(group);
                }
            }
            return lowTimeGroups;
        }

    }
}