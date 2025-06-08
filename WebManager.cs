using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HecticEscape
{
    public class WebManager : AManager
    {
        private readonly WebProxyHE _webProxy;
        private bool _disposed = false;

        public bool IsProxyRunning => _webProxy.IsProxyRunning;

        public event Action<bool>? ProxyStatusChanged;

        public WebManager(ConfigReader configReader, WebProxyHE webProxy)
            : base(configReader)
        {
            _webProxy = webProxy ?? throw new ArgumentNullException(nameof(webProxy));
            _webProxy.ProxyStatusChanged += status => ProxyStatusChanged?.Invoke(status);
            Initialize();
        }

        public override void Initialize()
        {
            Logger.Instance.Log("WebManager initialisiert", LogLevel.Info);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try
                {
                    (_webProxy as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Fehler beim Dispose von WebManager: {ex.Message}", LogLevel.Warn);
                }
            }
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}