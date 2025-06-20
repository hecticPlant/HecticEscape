using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HecticEscape
{
    public class GameManager : AManager
    {
        private readonly string SaveFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HecticEscape",
        "Games.json");
        private readonly ConfigReader _configReader;
        public List<SteamGame> InstalledGames { get; private set; } = new();

        public GameManager(ConfigReader configReader): base(configReader)
        {
            _configReader = configReader;
            Initialize();
        }


        public override void Initialize()
        {
            Logger.Instance.Log("GameManager initialisiert.", LogLevel.Info);
            LoadOrScanGames();
        }

        public List<SteamGame> GetInstalledGames()
        {
            List<SteamGame> games = SteamGameScanner.GetInstalledSteamGames();
            if (games == null || games.Count == 0)
            {
                Logger.Instance.Log("Keine installierten Spiele gefunden.", LogLevel.Info);
                return new List<SteamGame>();
            }
            Logger.Instance.Log($"Gefundene Spiele: {InstalledGames.Count}", LogLevel.Info);
            return games;
        }

        public void LoadOrScanGames()
        {
            if (File.Exists(SaveFile))
            {
                try
                {
                    string json = File.ReadAllText(SaveFile);
                    InstalledGames = JsonSerializer.Deserialize<List<SteamGame>>(json) ?? new();
                    Logger.Instance.Log($"{InstalledGames.Count} Spiele geladen.", LogLevel.Info);
                }
                catch
                {
                    Logger.Instance.Log("Fehler beim Laden der Spiele.", LogLevel.Error);
                    ScanAndSaveGames();
                }
            }
            else
            {
                ScanAndSaveGames();
            }
        }

        public void ScanAndSaveGames()
        {
            Logger.Instance.Log("Scanne Steam Spiele", LogLevel.Verbose);
            InstalledGames = SteamGameScanner.GetInstalledSteamGames();
            SaveGamesToFile();
        }

        public void SaveGamesToFile()
        {
            try
            {
                List<SteamGame> bestehendeSpiele = new();

                if (File.Exists(SaveFile))
                {
                    try
                    {
                        string jsonAlt = File.ReadAllText(SaveFile);
                        bestehendeSpiele = JsonSerializer.Deserialize<List<SteamGame>>(jsonAlt) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Log($"Warnung: Konnte bestehende Spiele nicht laden: {ex.Message}", LogLevel.Warn);
                    }
                }
                foreach (var spiel in InstalledGames)
                {
                    bool bereitsVorhanden = bestehendeSpiele.Any(x =>
                        string.Equals(x.ExecutablePath, spiel.ExecutablePath, StringComparison.OrdinalIgnoreCase));

                    if (!bereitsVorhanden)
                    {
                        bestehendeSpiele.Add(spiel);
                    }
                }
                string json = JsonSerializer.Serialize(bestehendeSpiele, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SaveFile, json);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Fehler beim Speichern der Spiele: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
