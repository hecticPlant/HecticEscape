﻿using System.Collections.Generic;
using System.Linq;

namespace HecticEscape
{
    /// <summary>
    /// Verwaltet alle Gruppen-Operationen (CRUD, Aktiv-Status).
    /// Greift auf die Gruppen-Datenstruktur in ConfigReader zu.
    /// </summary>
    public class GroupManager
    {
        private readonly ConfigReader _configReader;

        public GroupManager(ConfigReader configReader)
        {
            _configReader = configReader;
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
    }
}