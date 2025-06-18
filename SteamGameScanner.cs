using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public class SteamGame
{
    public string Name { get; set; }
    public string ExecutablePath { get; set; }
}

public static class SteamGameScanner
{
    public static List<SteamGame> GetInstalledSteamGames()
    {
        var games = new List<SteamGame>();

        string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString();
        if (string.IsNullOrEmpty(steamPath)) return games;

        var libraries = GetSteamLibraryFolders(steamPath);

        foreach (var lib in libraries)
        {
            string manifestDir = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(manifestDir)) continue;

            var manifestFiles = Directory.GetFiles(manifestDir, "appmanifest_*.acf");
            foreach (var manifest in manifestFiles)
            {
                try
                {
                    string content = File.ReadAllText(manifest);
                    string name = Regex.Match(content, @"""name""\s+""([^""]+)""").Groups[1].Value;
                    string installDir = Regex.Match(content, @"""installdir""\s+""([^""]+)""").Groups[1].Value;

                    string gameDir = Path.Combine(lib, "steamapps", "common", installDir);
                    if (Directory.Exists(gameDir))
                    {
                        var exes = Directory.GetFiles(gameDir, "*.exe", SearchOption.AllDirectories);
                        var mainExe = exes.FirstOrDefault();

                        if (mainExe != null)
                        {
                            games.Add(new SteamGame
                            {
                                Name = name,
                                ExecutablePath = mainExe
                            });
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return games;
    }

    private static List<string> GetSteamLibraryFolders(string steamPath)
    {
        var libraries = new List<string> { steamPath };
        string libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(libraryFile)) return libraries;

        var lines = File.ReadAllLines(libraryFile);
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^\s*""\d+""\s*""(.+?)""");
            if (match.Success)
            {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                libraries.Add(path);
            }
        }

        return libraries;
    }
}