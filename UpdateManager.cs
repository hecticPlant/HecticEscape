using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using HecticEscape;

public class UpdateManager
{
    private readonly string _repoOwner = "hecticPlant";
    private readonly string _repoName = "HecticEscape";
    private readonly HttpClient _httpClient = new();

    public async Task<string?> GetLatestVersionAsync()
    {
        Logger.Instance.Log("UpdateManager: Starte Versionsabfrage bei GitHub.", LogLevel.Info);
        var url = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("request");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var version = doc.RootElement.GetProperty("tag_name").GetString();
            Logger.Instance.Log($"UpdateManager: Gefundene Version: {version}", LogLevel.Info);
            return version;
        }
        catch (Exception ex)
        {
            Logger.Instance.Log($"UpdateManager: Fehler bei der Versionsabfrage: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    public async Task DownloadLatestReleaseAssetAsync(string assetName, string downloadPath)
    {
        Logger.Instance.Log($"UpdateManager: Starte Download des Release-Assets '{assetName}'.", LogLevel.Info);
        var url = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("request");

        try
        {
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var assets = doc.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == assetName)
                {
                    var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    Logger.Instance.Log($"UpdateManager: Download-URL gefunden: {downloadUrl}", LogLevel.Debug);
                    var assetRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    assetRequest.Headers.UserAgent.ParseAdd("request");

                    var assetResponse = await _httpClient.SendAsync(assetRequest);
                    assetResponse.EnsureSuccessStatusCode();

                    using var fileStream = File.Create(downloadPath);
                    await assetResponse.Content.CopyToAsync(fileStream);
                    Logger.Instance.Log($"UpdateManager: Asset '{assetName}' erfolgreich heruntergeladen nach '{downloadPath}'.", LogLevel.Info);
                    return;
                }
            }
            Logger.Instance.Log($"UpdateManager: Asset '{assetName}' nicht gefunden.", LogLevel.Error);
            throw new Exception("Asset nicht gefunden.");
        }
        catch (Exception ex)
        {
            Logger.Instance.Log($"UpdateManager: Fehler beim Download des Assets: {ex.Message}", LogLevel.Error);
            throw;
        }
    }

    /// <summary>
    /// Startet das heruntergeladene Update und beendet die aktuelle Anwendung.
    /// </summary>
    public void ApplyUpdate(string installerPath)
    {
        Logger.Instance.Log($"UpdateManager: Starte Installer '{installerPath}' im Silent-Mode.", LogLevel.Info);
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT",
                    UseShellExecute = true
                }
            };
            process.Start();
            Logger.Instance.Log("UpdateManager: Installer gestartet. Anwendung wird beendet.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Logger.Instance.Log($"UpdateManager: Fehler beim Starten des Installers: {ex.Message}", LogLevel.Error);
        }

        // Anwendung beenden, damit das Update durchgeführt werden kann
        System.Windows.Application.Current.Shutdown();
    }
}