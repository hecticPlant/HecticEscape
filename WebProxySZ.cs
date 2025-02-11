using System.Text;
using System.Diagnostics;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace ScreenZen
{
    /// <summary>
    /// Blockiert bestimmte Domains über einen lokalen Proxy-Server.
    /// </summary>
    public class WebProxySZ
    {
        private List<string> blockedDomains = new List<string>();
        private readonly ProxyServer proxy;
        private readonly ExplicitProxyEndPoint proxyEndPoint;
        private bool isProxyRunning;
        public event Action<bool> ProxyStatusChanged;

        /// <summary>
        /// Konstruktor: Initialisiert den Proxy-Server mit HTTPS-Unterstützung.
        /// </summary>
        public WebProxySZ()
        {
            proxy = new ProxyServer();
            proxy.BeforeRequest += OnRequestAsync;
            proxy.BeforeResponse += OnResponseAsync;

            proxy.CertificateManager.CreateRootCertificate(true);
            proxy.CertificateManager.TrustRootCertificate();

            proxyEndPoint = new ExplicitProxyEndPoint(System.Net.IPAddress.Any, 8888, true);
            proxy.AddEndPoint(proxyEndPoint);

            isProxyRunning = false;
            Logger.Instance.Log("WebProxySZ initialisiert.");
        }

        public bool IsProxyRunning
        {
            get => isProxyRunning;
            private set
            {
                if (isProxyRunning != value)
                {
                    isProxyRunning = value;
                    ProxyStatusChanged?.Invoke(isProxyRunning);
                    Logger.Instance.Log($"IsProxyRunning geändert: {isProxyRunning}");
                }
            }
        }

        public async Task StartProxy()
        {
            if (!isProxyRunning)
            {
                Logger.Instance.Log($"Starte Proxy mit {blockedDomains.Count} Domains.");
                await Task.Run(() => proxy.Start());
                isProxyRunning = true;
                proxy.SetAsSystemHttpProxy(proxyEndPoint);
                proxy.SetAsSystemHttpsProxy(proxyEndPoint);
            }
        }

        public async Task StopProxy()
        {
            if (isProxyRunning)
            {
                await Task.Run(() => proxy.Stop());
                isProxyRunning = false;
                Logger.Instance.Log("Proxy gestoppt.");
            }
        }

        private async Task OnRequestAsync(object sender, SessionEventArgs e)
        {
            string requestUrl = e.HttpClient.Request.Url;
            Logger.Instance.Log($"Anfrage erhalten: {requestUrl}");

            if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
                return;

            string requestHost = uri.Host.ToLowerInvariant();
            foreach (var domain in blockedDomains)
            {
                if (requestHost.Contains(domain.ToLowerInvariant()))
                {
                    Logger.Instance.Log($"Blockiere {requestUrl}");
                    var blockMessage = "Zugriff auf diese Website ist blockiert.";
                    var responseBytes = Encoding.UTF8.GetBytes(blockMessage);
                    e.Ok(responseBytes);
                    e.HttpClient.Response.StatusCode = 403;
                    e.HttpClient.Response.StatusDescription = "Forbidden";
                    e.HttpClient.Response.ContentType = "text/plain";
                    break;
                }
            }
        }

        /// <summary>
        /// Setzt die Liste der zu blockierenden Domains und wendet Sperrmaßnahmen an.
        /// </summary>
        public void SetBlockedDomains(List<string> domains)
        {
            Logger.Instance.Log($"Liste aktualisiert mit {domains.Count} Domains.");
            blockedDomains = domains;

            TerminateExistingConnections();
            FlushDnsCache();
            //BlockWithFirewall();
        }

        /// <summary>
        /// Erzwingt die Nutzung des lokalen Proxys im System.
        /// </summary>
        private void EnforceSystemProxy()
        {
            Logger.Instance.Log("Erzwinge Proxy-Nutzung.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/C reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 1 /f",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/C reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyServer /t REG_SZ /d \"127.0.0.1:8888\" /f",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        /// <summary>
        /// Setzt die Proxy-Einstellungen auf Standard zurück.
        /// </summary>
        private void ResetSystemProxy()
        {
            Logger.Instance.Log("Setze System-Proxy zurück.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/C reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /f",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        /// <summary>
        /// Beendet bestehende Verbindungen zu geblockten Domains.
        /// </summary>
        private void TerminateExistingConnections()
        {
            Logger.Instance.Log("Beende bestehende Verbindungen zu geblockten Domains.");
            foreach (var domain in blockedDomains)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/C netstat -ano | findstr {domain} | ForEach-Object {{taskkill /PID $_.Split()[4] /F}}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        /// <summary>
        /// Leert den DNS-Cache.
        /// </summary>
        private void FlushDnsCache()
        {
            Logger.Instance.Log("Leere den DNS-Cache.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/C ipconfig /flushdns",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        /// <summary>
        /// Setzt Windows-Firewall-Regeln für gesperrte Domains.
        /// </summary>
        private void BlockWithFirewall()
        {
            Logger.Instance.Log("Setze Firewall-Regeln.");
            foreach (var domain in blockedDomains)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/C netsh advfirewall firewall add rule name=\"Block {domain}\" dir=out action=block remoteip={GetIpFromDomain(domain)}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        private string GetIpFromDomain(string domain)
        {
            return "0.0.0.0"; // Platzhalter: Hier eine Methode zur DNS-Auflösung einfügen
        }

        private async Task OnResponseAsync(object sender, SessionEventArgs e)
        {
            if (e.HttpClient.Response.StatusCode == 200)
            {
                await e.GetResponseBody();
            }
        }
    }
}
