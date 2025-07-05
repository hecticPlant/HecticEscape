using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HecticEscape
{
    public class WebManager : AManager
    {

        private HttpListener? listener;
        private CancellationTokenSource? cts;
        private bool redirectEnabled = false;  
        private List<string> redirectList = new List<string>();
        private List<string> openDomains = new List<string>();
        private object lockObj = new();          
        public bool isListening => listener != null && listener.IsListening;
        private readonly GroupManager _groupManager;
        public WebManager(ConfigReader configReader, GroupManager groupManager) : base(configReader)
        {
            _groupManager = groupManager;
            Initialize();
        }
        public override void Initialize()
        {
            if(_configReader.GetWebsiteBlockingEnabled())
            {
                StartRedirectServer();
                EnableRedirect();
                Logger.Instance.Log("WebManager initialisiert und Redirect-Server gestartet.", LogLevel.Info);
            }
        }


        public void StartRedirectServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:12345/api/redirectList/");
            listener.Prefixes.Add("http://localhost:12345/api/openTabs/");
            listener.Start();

            cts = new CancellationTokenSource();
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                Logger.Instance.Log("Server läuft auf http://localhost:12345/api", LogLevel.Info);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await listener.GetContextAsync();

                        if (context.Request.HttpMethod == "GET" &&
                            context.Request.Url.AbsolutePath == "/api/redirectList/")
                        {
                            bool enabled;
                            List<string> domains;
                            lock (lockObj)
                            {
                                enabled = redirectEnabled;
                                domains = new List<string>(redirectList);
                            }

                            var responseObj = new
                            {
                                enabled = enabled,
                                redirectDomains = domains
                            };

                            var json = JsonSerializer.Serialize(responseObj);
                            var buffer = Encoding.UTF8.GetBytes(json);

                            context.Response.ContentType = "application/json";
                            context.Response.ContentLength64 = buffer.Length;
                            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            context.Response.OutputStream.Close();
                        }
                        else if (context.Request.HttpMethod == "POST" &&
                                 context.Request.Url.AbsolutePath == "/api/openTabs/")
                        {
                            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                            var body = await reader.ReadToEndAsync();

                            var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(body);

                            lock (lockObj)
                            {
                                openDomains = data != null && data.TryGetValue("openDomains", out var domains) ? domains : new List<string>();
                            }
                            context.Response.StatusCode = 200;
                            context.Response.Close();
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                            context.Response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                            Logger.Instance.Log($"Fehler im RedirectServer: {ex.Message}", LogLevel.Error);
                    }
                }
            }, token);
        }


        public async Task StopRedirectServer()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }

            if (listener != null)
            {
                listener.Stop();
                listener.Close();
                listener = null;
            }

            await Task.Delay(200);
        }

        public void EnableRedirect()
        {
            lock (lockObj)
            {
                redirectEnabled = true;
            }
        }

        public void DisableRedirect()
        {
            lock (lockObj)
            {
                redirectEnabled = false;
            }
        }

        public List<Website> GetWebsitesFromGroup(Gruppe gruppe)
        {
            return gruppe?.Websites ?? new List<Website>();
        }

        public void AddWebsiteToGroup(Gruppe group, string domainName)
        {
            Logger.Instance.Log($"Gruppe '{domainName}' wird zur Gruppe '{group?.Name}' hinzuzufügt.", LogLevel.Info);
            if (group == null || string.IsNullOrWhiteSpace(domainName))
                return;

            var website = new Website
            {
                Name = domainName,
                DailyTimeMs = 7200000, // Standard: 2 Stunden
                Logs = new List<Log>()
            };

            if (!group.Websites.Any(a => a.Name.Equals(website.Name, StringComparison.OrdinalIgnoreCase)))
            {
                group.Websites.Add(website);
            }
            _configReader.SetSaveConfigFlag();
        }

        public bool RemoveWebsiteFromGroup(Gruppe group, Website website)
        {
            Logger.Instance.Log($"Versuche, App '{website?.Name}' aus Gruppe '{group?.Name}' zu entfernen.", LogLevel.Verbose);
            if (group == null || website == null)
                return false;

            var existingWebsite = group.Websites.FirstOrDefault(w => w == website);
            if (existingWebsite != null)
            {
                group.Websites.Remove(existingWebsite);
                _configReader.SetSaveConfigFlag();
                return true;
            }
            return false;
        }

        public Website GetWebsiteByName(Gruppe group, string websiteName)
        {
            return group?.Websites.FirstOrDefault(a => a.Name.Equals(websiteName, StringComparison.OrdinalIgnoreCase));
        }

        public void SetTimeMS(Gruppe group, Website website, long timeMs)
        {
            if (group == null || website == null) return;
            var existingWebsite = group.Websites.FirstOrDefault(a => a == website);
            DateOnly date = DateOnly.FromDateTime(DateTime.Now);
            if (existingWebsite != null)
            {
                Log? log = existingWebsite.Logs.FirstOrDefault(l => l.Date == date);
                if (log == null)
                {
                    log = new Log { Date = date, TimeMs = 0 };
                    existingWebsite.Logs.Add(log);
                    Logger.Instance.Log($"Neues Log für {existingWebsite.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Debug);
                }
                else
                {
                    log.TimeMs = timeMs;
                }
            }
            _configReader.SetSaveConfigFlag();
        }

        public void SetDailyTimeMs(Gruppe group, Website website, long dailyTimeMs)
        {
            if (group == null || website == null) return;
            var existingWebsite = group.Websites.FirstOrDefault(w => w == website);
            if (existingWebsite != null)
            {
                existingWebsite.DailyTimeMs = dailyTimeMs;
            }
            _configReader.SetSaveConfigFlag();
        }

        public long GetDailyTimeLeft(Gruppe group, Website website, DateOnly date)
        {
            if (group == null || website == null) return 0;
            var log = website.Logs?.FirstOrDefault(l => l.Date == date);
            long used = log?.TimeMs ?? 0;
            return website.DailyTimeMs - used;
        }

        private List<string> GetAllActiveWebsites()
        {
            List<string> websiteList = new List<string>();
            foreach (var group in _groupManager.GetAllActiveGroups())
            {
                foreach (var website in group.Websites)
                {
                    websiteList.Add(website.Name);
                }
            }
            return websiteList;
        }

        public async Task BlockHandler(int intervalCheckMs, bool isBreakActive)
        {
            var activeGroups = _groupManager.GetAllActiveGroups();
            var newRedirectList = new List<string>();

            if (!_configReader.GetAppBlockingEnabled() && !_configReader.GetWebsiteBlockingEnabled())
            {
                return;
            }

            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            foreach (var group in activeGroups)
            {
                foreach (var website in group.Websites)
                {
                    if (website.Logs == null)
                    {
                        Logger.Instance.Log($"Erstelle neues Log für {website.Name} in Gruppe {group.Name}, da Logs null sind.", LogLevel.Error);
                        website.Logs = new List<Log>();
                    }

                    var log = website.Logs.FirstOrDefault(l => l.Date == today);
                    if (log == null)
                    {
                        log = new Log { Date = today, TimeMs = 0 };
                        website.Logs.Add(log);
                        Logger.Instance.Log($"Neues Log für {website.Name} am {today:yyyy-MM-dd} erstellt.", LogLevel.Info);
                    }
                    if (isBreakActive)
                    {
                        bool isOpen = IsWebsiteOpen(website.Name);

                        if (isOpen)
                        {
                            newRedirectList.Add(website.Name);
                        }
                        continue;
                    }

                    if (IsWebsiteOpen(website.Name))
                    {
                        AddTimeToLog(website, today, intervalCheckMs);
                        _groupManager.AddTimeToLog(group, today, intervalCheckMs);
                         if (log.TimeMs >= website.DailyTimeMs)
                        {
                            newRedirectList.Add(website.Name);
                        }
                    }
                }
            }

            lock (lockObj)
            {
                redirectList = newRedirectList.Distinct().ToList();
            }
        }



        public bool IsWebsiteOpen(string websiteName)
        {
            lock (lockObj)
            {
                var target = websiteName.ToLowerInvariant();
                bool result = openDomains.Any(domain =>
                {
                    var d = domain.ToLowerInvariant();
                    bool contains = d.Contains(target) || d == target;
                    return contains;
                });
                return result;
            }
        }


        public void AddTimeToLog(Website werbsite, DateOnly date, long timeMs)
        {
            if (werbsite == null || date == default || timeMs < 0)
            {
                Logger.Instance.Log("Ungültige Parameter für AddTimeToLog. Website, Datum oder Zeit sind ungültig.", LogLevel.Error);
                return;
            }
            var log = werbsite.Logs.FirstOrDefault(l => l.Date == date);
            if (log == null)
            {
                log = new Log { Date = date, TimeMs = 0 };
                werbsite.Logs.Add(log);
                Logger.Instance.Log($"Neues Log für {werbsite.Name} am {date:yyyy-MM-dd} erstellt.", LogLevel.Info);
            }
            log.TimeMs += timeMs;
            _configReader.SetSaveConfigFlag();
        }
    }
}