using HecticEscape;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

public class UpdateManager
{
    private readonly string _repoOwner = "hecticPlant";
    private readonly string _repoName = "HecticEscape";
    private readonly HttpClient _httpClient = new();
 
    public async Task<string?> GetLatestVersionAsync()
    {
        Logger.Instance.Log("UpdateManager: Starte Versionsabfrage bei GitHub.", LogLevel.Info);
        var url = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases";
        Logger.Instance.Log($"UpdateManager: Abfrage-URL: {url}", LogLevel.Debug);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("HecticEscape-Updater");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                Logger.Instance.Log("UpdateManager: Keine Releases gefunden.", LogLevel.Warn);
                return null;
            }

            var latestRelease = doc.RootElement.EnumerateArray()
                .OrderByDescending(e => e.GetProperty("created_at").GetDateTime())
                .FirstOrDefault();

            if (latestRelease.ValueKind != JsonValueKind.Object || !latestRelease.TryGetProperty("tag_name", out var tagElement))
            {
                Logger.Instance.Log("UpdateManager: Kein gültiger Release gefunden.", LogLevel.Warn);
                return null;
            }

            var version = tagElement.GetString();
            Logger.Instance.Log($"UpdateManager: Gefundene Version: {version}", LogLevel.Info);
            return version;
        }
        catch (Exception ex)
        {
            Logger.Instance.Log($"UpdateManager: Fehler bei der Versionsabfrage: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

}