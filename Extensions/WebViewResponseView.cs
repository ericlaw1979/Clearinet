using Clearinet;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Extensions
{
    internal interface IWebPreviewRenderer : IDisposable
    {
        Control View { get; }
        event EventHandler<WebPreviewResultEventArgs> PreviewCompleted;
        Task InitializeAsync();
        void Render(WebPreviewDocument document);
        void Clear();
    }

    internal sealed class WebPreviewResultEventArgs : EventArgs
    {
        internal WebPreviewDocument Document { get; }
        internal string Error { get; }
        internal bool Fatal { get; }

        internal WebPreviewResultEventArgs(WebPreviewDocument document, string error = null, bool fatal = false)
        {
            Document = document;
            Error = error;
            Fatal = fatal;
        }
    }

    internal sealed class WebViewResponseView : UserControl
    {
        private readonly Panel previewPanel;
        private readonly Label message;
        private IWebPreviewRenderer renderer;
        private WebPreviewDocument document;
        private bool initializing;
        private bool ready;
        private string unavailableReason;

        internal WebViewResponseView()
        {
            /*var notice = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(6),
                Text = "Offline preview: scripts, navigation, forms, downloads, and external resources are blocked."
            };*/
            message = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Padding = new Padding(12),
                Text = "No response selected."
            };
            previewPanel = new Panel { Dock = DockStyle.Fill };
            previewPanel.Controls.Add(message);
            Controls.Add(previewPanel);
            //Controls.Add(notice);
            BackColor = CONFIG.colorDisabledEdit;
        }

        internal void Assign(HTTPResponseHeaders headers, byte[] body)
        {
            Clear();
            if (headers == null || body == null || body.Length == 0)
            {
                ShowMessage("No response body to preview.");
                return;
            }
            string contentType = headers["Content-Type"];
            if (!WebPreviewDocument.IsSupported(contentType))
            {
                ShowMessage("No passive WebView preview for Content-Type: " + contentType);
                return;
            }
            try
            {
                document = WebPreviewDocument.Create(contentType, Exchange.DecodeBodyFromHeaders(headers, body));
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is NotSupportedException || ex is FormatException)
            {
                CApp.Log.Log("WebView response decoding failed: " + ex.Message);
                ShowMessage("Unable to decode this response for preview.\r\n" + ex.Message);
                return;
            }
            EnsurePreview();
        }

        internal void Clear()
        {
            document = null;
            renderer?.Clear();
            ShowMessage("No response selected.");
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) EnsurePreview();
        }

        private async void EnsurePreview()
        {
            if (IsDisposed || Disposing || document == null || !Visible) return;
            if (unavailableReason != null)
            {
                ShowMessage(unavailableReason);
                return;
            }
            if (initializing) return;
            try
            {
                if (!ready)
                {
                    initializing = true;
                    ShowMessage("Loading the system Microsoft Edge WebView2 runtime...");
                    renderer = CreateRenderer();
                    renderer.PreviewCompleted += PreviewCompleted;
                    renderer.View.Dock = DockStyle.Fill;
                    renderer.View.Visible = false;
                    previewPanel.Controls.Add(renderer.View);
                    await renderer.InitializeAsync();
                    if (IsDisposed || Disposing) return;
                    ready = true;
                }
                if (document != null && Visible)
                {
                    ShowMessage("Rendering response (offline)...");
                    renderer.Render(document);
                }
            }
            catch (Exception ex) when (IsWebViewFailure(ex))
            {
                if (!IsDisposed && !Disposing) ShowUnavailable(ex.Message);
            }
            finally
            {
                initializing = false;
            }
        }

        // Keep optional SDK assembly resolution inside the caller's failure boundary.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IWebPreviewRenderer CreateRenderer() => new WebViewRenderer();

        private static bool IsWebViewFailure(Exception ex) =>
            ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException ||
            ex is DllNotFoundException || ex is BadImageFormatException || ex is TypeLoadException ||
            ex is EntryPointNotFoundException || ex is COMException || ex is NotSupportedException ||
            ex is NotImplementedException || ex is InvalidOperationException || ex is ArgumentException ||
            ex is System.ComponentModel.Win32Exception;

        private void PreviewCompleted(object sender, WebPreviewResultEventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (e.Fatal)
            {
                ShowUnavailable(e.Error);
                return;
            }
            if (!ReferenceEquals(e.Document, document)) return;
            if (e.Error != null)
            {
                ShowMessage(e.Error);
                return;
            }
            message.Visible = false;
            renderer.View.Visible = true;
            renderer.View.BringToFront();
        }

        private void ShowUnavailable(string reason)
        {
            unavailableReason = "WebView preview is unavailable.\r\nInstall or repair the Microsoft Edge WebView2 Evergreen Runtime " +
                "and restart Clearinet. Other inspectors remain available.\r\n\r\n" + reason;
            CApp.Log.Log("WebView preview unavailable: " + reason);
            ready = false;
            if (renderer != null)
            {
                renderer.PreviewCompleted -= PreviewCompleted;
                renderer.Dispose();
                renderer = null;
            }
            ShowMessage(unavailableReason);
        }

        private void ShowMessage(string text)
        {
            if (renderer != null) renderer.View.Visible = false;
            message.Text = text;
            message.Visible = true;
            message.BringToFront();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && renderer != null)
            {
                renderer.PreviewCompleted -= PreviewCompleted;
                renderer.Dispose();
                renderer = null;
            }
            base.Dispose(disposing);
        }
    }
}
