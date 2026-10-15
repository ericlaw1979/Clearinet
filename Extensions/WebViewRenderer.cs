using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Extensions
{
    internal sealed class WebViewRenderer : UserControl, IWebPreviewRenderer
    {
        private readonly WebView2 browser;
        private CoreWebView2Environment environment;
        private WebPreviewDocument document;
        private bool allowNavigation;
        private ulong navigationId;
        private bool ready;

        public Control View => this;
        public event EventHandler<WebPreviewResultEventArgs> PreviewCompleted;

        internal WebViewRenderer()
        {
            browser = new WebView2 { Dock = DockStyle.Fill, AllowExternalDrop = false };
            Controls.Add(browser);
        }

        public async Task InitializeAsync()
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions
                {
                    AllowSingleSignOnUsingOSPrimaryAccount = false,
                    AreBrowserExtensionsEnabled = false,
                    ReleaseChannels = CoreWebView2ReleaseChannels.Stable,
                    // Speculative DNS/pings are not ordinary WebResourceRequested events.
                    AdditionalBrowserArguments = "--disable-background-networking --dns-prefetch-disable --no-pings " +
                        "--autoplay-policy=user-gesture-required --host-resolver-rules=\"MAP * ~NOTFOUND\""
                };
                string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clearinet", "WebViewPreview");
                environment = await CoreWebView2Environment.CreateAsync(null, dataFolder, options);
                if (IsDisposed || Disposing) return;
                CoreWebView2ControllerOptions controller = environment.CreateCoreWebView2ControllerOptions();
                controller.IsInPrivateModeEnabled = true;
                controller.ProfileName = "Preview" + Guid.NewGuid().ToString("N");
                await browser.EnsureCoreWebView2Async(environment, controller);
                if (IsDisposed || Disposing) return;

                CoreWebView2 core = browser.CoreWebView2;
                CoreWebView2Settings settings = core.Settings;
                settings.IsScriptEnabled = false;
                settings.AreDefaultScriptDialogsEnabled = false;
                settings.IsWebMessageEnabled = false;
                settings.AreHostObjectsAllowed = false;
                settings.AreDevToolsEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsBuiltInErrorPageEnabled = false;
                settings.IsSwipeNavigationEnabled = false;
                settings.IsNonClientRegionSupportEnabled = false;
                // Do not submit captured content to online reputation services.
                settings.IsReputationCheckingRequired = false;
                core.IsMuted = false;

                core.NavigationStarting += NavigationStarting;
                core.NavigationCompleted += NavigationCompleted;
                core.FrameNavigationStarting += (sender, e) => e.Cancel = true;
                core.NewWindowRequested += (sender, e) => e.Handled = true;
                core.LaunchingExternalUriScheme += (sender, e) => e.Cancel = true;
                core.DownloadStarting += (sender, e) => { e.Cancel = true; e.Handled = true; };
                core.PermissionRequested += (sender, e) => { e.State = CoreWebView2PermissionState.Deny; e.SavesInProfile = false; };
                core.BasicAuthenticationRequested += (sender, e) => e.Cancel = true;
                core.ClientCertificateRequested += (sender, e) => { e.Cancel = true; e.Handled = true; };
                core.ServerCertificateErrorDetected += (sender, e) => e.Action = CoreWebView2ServerCertificateErrorAction.Cancel;
                core.ProcessFailed += (sender, e) => PreviewCompleted?.Invoke(this,
                    new WebPreviewResultEventArgs(document, "The WebView2 process failed: " + e.ProcessFailedKind, true));
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += WebResourceRequested;
                ready = true;
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                throw new NotSupportedException("The Microsoft Edge WebView2 Evergreen Runtime could not be loaded.", ex);
            }
        }

        public void Render(WebPreviewDocument preview)
        {
            if (!ready) throw new InvalidOperationException("The passive WebView is not initialized.");
            Clear();
            document = preview;
            allowNavigation = true;
            browser.CoreWebView2.Navigate(preview.Url);
        }

        public void Clear()
        {
            document = null;
            allowNavigation = false;
            navigationId = 0;
            if (ready) browser.CoreWebView2.Stop();
        }

        private void NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // WebView2 can mark Navigate() as user-initiated; only the one-shot synthetic URL is authoritative.
            if (!allowNavigation || document == null || e.IsRedirected ||
                !string.Equals(e.Uri, document.Url, StringComparison.Ordinal))
            {
                e.Cancel = true;
                return;
            }
            // Only the single host-initiated navigation is permitted, not a refresh or link to the same URL.
            allowNavigation = false;
            navigationId = e.NavigationId;
        }

        private void NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (document == null || e.NavigationId != navigationId) return;
            PreviewCompleted?.Invoke(this, new WebPreviewResultEventArgs(document,
                e.IsSuccess ? null : "Unable to render this response: " + e.WebErrorStatus));
        }

        private void WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (document != null && navigationId != 0 && e.ResourceContext == CoreWebView2WebResourceContext.Document &&
                string.Equals(e.Request.Method, "GET", StringComparison.Ordinal) && string.Equals(e.Request.Uri, document.Url, StringComparison.Ordinal))
            {
                e.Response = environment.CreateWebResourceResponse(new MemoryStream(document.Content, false), 200, "OK", document.ResponseHeaders);
            }
            else
            {
                // This includes relative URLs, remote URLs, file URLs, and worker requests. Never fall through to the network.
                e.Response = environment.CreateWebResourceResponse(null, 403, "Blocked by offline preview", "Cache-Control: no-store\r\n");
            }
        }
    }
}
