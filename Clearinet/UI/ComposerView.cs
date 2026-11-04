using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class ComposerView : UserControl
    {
        private bool updatingRequestLayout;

        public ComposerView()
        {
            InitializeComponent();
            cbxHTTPVersion.SelectedIndex = 0;
            cbxMethod.SelectedIndex = 0;

            // SetCueText accesses Handle; wait until parenting establishes the target monitor DPI.
            txtURL.HandleCreated += (sender, e) => Win32UI.SetCueText(txtURL, "Request URL");
            txtURL.Text = "https://debugtheweb.com/echo2.aspx";
            cbxMethod.TextChanged += (sender, e) => UpdateRequestLayout();
            cbxHTTPVersion.TextChanged += (sender, e) => UpdateRequestLayout();
            cbxMethod.FontChanged += (sender, e) => UpdateRequestLayout();
            cbxHTTPVersion.FontChanged += (sender, e) => UpdateRequestLayout();
            btnSend.FontChanged += (sender, e) => UpdateRequestLayout();
            UpdateRequestLayout();
        }

        private int PreferredComboWidth(ComboBox combo)
        {
            int width = TextRenderer.MeasureText(combo.Text, combo.Font).Width;
            foreach (object item in combo.Items)
                width = Math.Max(width, TextRenderer.MeasureText(combo.GetItemText(item), combo.Font).Width);
            return width + SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi)
                + LogicalToDeviceUnits(12);
        }

        private void UpdateRequestLayout()
        {
            if (updatingRequestLayout || layoutRequest == null || layoutRequest.ColumnStyles.Count != 4) return;
            updatingRequestLayout = true;
            try
            {
                int available = ClientSize.Width - Padding.Horizontal - layoutRequest.Padding.Horizontal;
                if (available <= 0) return;
                int methodWidth = PreferredComboWidth(cbxMethod) + cbxMethod.Margin.Horizontal;
                int versionWidth = PreferredComboWidth(cbxHTTPVersion) + cbxHTTPVersion.Margin.Horizontal;
                int sendWidth = btnSend.GetPreferredSize(Size.Empty).Width + btnSend.Margin.Horizontal;
                // Only this usability threshold is a canonical 96-DPI dimension. Content widths and
                // margins are already in current device units; scaling them again moves the breakpoint.
                bool wrap = available < methodWidth + versionWidth + sendWidth
                    + LogicalToDeviceUnits(240) + txtURL.Margin.Horizontal;
                if (wrap)
                {
                    int comboSpace = Math.Max(0, available - sendWidth);
                    if (comboSpace < methodWidth + versionWidth)
                    {
                        methodWidth = (int)((long)comboSpace * methodWidth / (methodWidth + versionWidth));
                        versionWidth = comboSpace - methodWidth;
                    }
                }

                int versionColumn = wrap ? 1 : 2;
                int urlRow = wrap ? 1 : 0;
                int urlColumn = wrap ? 0 : 1;
                int urlSpan = wrap ? 4 : 1;
                float column1 = wrap ? versionWidth : 100f;
                float column2 = wrap ? 100f : versionWidth;
                SizeType type1 = wrap ? SizeType.Absolute : SizeType.Percent;
                SizeType type2 = wrap ? SizeType.Percent : SizeType.Absolute;
                if (layoutRequest.GetRow(txtURL) == urlRow && layoutRequest.GetColumnSpan(txtURL) == urlSpan
                    && layoutRequest.ColumnStyles[0].Width == methodWidth
                    && layoutRequest.ColumnStyles[1].SizeType == type1 && layoutRequest.ColumnStyles[1].Width == column1
                    && layoutRequest.ColumnStyles[2].SizeType == type2 && layoutRequest.ColumnStyles[2].Width == column2
                    && layoutRequest.ColumnStyles[3].SizeType == SizeType.Absolute && layoutRequest.ColumnStyles[3].Width == sendWidth)
                    return;

                // Keep controls parented and update only changed geometry. A deferred resize callback
                // would leave the first lazy activation stale; the guard prevents nested layout churn.
                layoutRequest.SuspendLayout();
                try
                {
                    layoutRequest.SetColumnSpan(txtURL, 1);
                    layoutRequest.SetCellPosition(txtURL, new TableLayoutPanelCellPosition(urlColumn, urlRow));
                    layoutRequest.SetCellPosition(cbxHTTPVersion, new TableLayoutPanelCellPosition(versionColumn, 0));
                    layoutRequest.SetColumnSpan(txtURL, urlSpan);
                    layoutRequest.ColumnStyles[0].Width = methodWidth;
                    layoutRequest.ColumnStyles[1].SizeType = type1;
                    layoutRequest.ColumnStyles[1].Width = column1;
                    layoutRequest.ColumnStyles[2].SizeType = type2;
                    layoutRequest.ColumnStyles[2].Width = column2;
                    layoutRequest.ColumnStyles[3].SizeType = SizeType.Absolute;
                    layoutRequest.ColumnStyles[3].Width = sendWidth;
                }
                finally { layoutRequest.ResumeLayout(true); }
            }
            finally { updatingRequestLayout = false; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            // Choose rows using the imminent docked width before the parent's dock pass measures
            // the table height. Changing rows inside the table's Layout event leaves stale dock bounds.
            UpdateRequestLayout();
            base.OnLayout(e);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            UpdateRequestLayout();
        }

        private void btnSend_Click(object sender, System.EventArgs e)
        {
            try
            {
                if (!txtURL.Text.OICStartsWith("http"))
                {
                    txtURL.Text = "https://" + txtURL.Text.Trim();
                }
                cbxMethod.Text = cbxMethod.Text.Trim().ToUpperInvariant();

                var bHeadersFixed = false;

                // Fixup header block, looking for empty lines, missing colon, etc
                List<String> lines = new List<string>();
                foreach (string line in txtHeaders.Lines)
                {
                    if (line.Trim().Length == 0)
                    {
                        bHeadersFixed = true;
                        continue;
                    }
                    if (!line.Contains(":"))
                    {
                        bHeadersFixed = true;
                        continue;
                    }
                    if (line.OICStartsWithAny(" ", "\t"))
                    {
                        lines.Add(line.Trim());
                        bHeadersFixed = true;
                        continue;
                    }
                    lines.Add(line);
                }

                var (method, path, version, headerList, origin) =
                    Parser.ParseRequestHeaders(
                        $"{cbxMethod.Text} {txtURL.Text} {cbxHTTPVersion.Text}\r\n" +
                        string.Join("\r\n", lines));

                HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList)
                {
                    HTTPVersion = version,
                    HTTPMethod = method
                };

                bool bBase64Body = rqh.ExistsAndContains("Clearinet-Composer-Body-Base64", "true");
                if ((origin != null) && origin.Contains(":"))
                {
                    rqh.UriScheme = origin.TrimAfter(":");
                    if (!rqh.Exists("Host"))
                    {
                        rqh["Host"] = origin.TrimBefore("://");
                        bHeadersFixed = true;
                    }
                }

                byte[] arrBody;

                if (!bBase64Body)
                {
                    arrBody = Encoding.UTF8.GetBytes(txtBody.Text);
                }
                else
                {
                    arrBody = Convert.FromBase64String(txtBody.Text);
                }
                if (arrBody.Length > 0)
                {
                    rqh["Content-Length"] = arrBody.Length.ToString();
                    bHeadersFixed = true;
                }

                if (bHeadersFixed)
                {
                    txtHeaders.Text = rqh.ToString(false, false);
                }
                if (bBase64Body)
                {
                    rqh.Remove("Clearinet-Composer-Body-Base64");
                }

                Exchange XC = new Exchange(rqh, arrBody)
                {
                    BitFlags = ExchangeFlags.RequestGeneratedByClearinet
                };
                XC.EnsureID();

                if ((Control.ModifierKeys & Keys.Shift) != 0) XC["x-breakrequest"] = "Shift+Click";
                XC.state = ExchangeState.AutoTamperRequestBefore;
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Composer Failed");
            }
        }

        private void ComposerView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Clearinet.Exchange[]"))
            {
                e.Effect = DragDropEffects.Copy; return;
            }
        }

        private void ComposerView_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Clearinet.Exchange[]"))
            {
                Exchange[] arrXC = null;
                try
                {
                    arrXC = (Exchange[])e.Data.GetData("Clearinet.Exchange[]");
                }
                catch (InvalidCastException)
                {
                    CApp.alert("Cross process drops are not yet supported.");
                }
                catch
                {
                    return;
                }

                if (arrXC?.Length < 1) return;
                Exchange XC = arrXC[0];
                PopulateFromExchange(XC);
            }
        }

        internal void PopulateFromExchange(Exchange XC)
        {
            txtHeaders.Text = XC.RequestHeaders?.ToString(false, false);
            if (XC.RequestBody.HasData())
            {
                if (XC.RequestBody.HasNonTextData())
                {
                    txtBody.Text = Convert.ToBase64String(XC.RequestBody);
                    txtHeaders.Text += "Clearinet-Composer-Body-Base64: true\r\n";
                }
                else
                {
                    txtBody.Text = XC.GetRequestBodyAsString();
                }
            }

            txtURL.Text = XC.fullUrl;
            cbxMethod.Text = XC.RequestHeaders?.HTTPMethod ?? "GET";
            cbxHTTPVersion.Text = XC.RequestHeaders?.HTTPVersion ?? "HTTP/1.1";
        }

        private void txtURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend_Click(sender, e);
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void cbxMethod_TextChanged(object sender, EventArgs e)
        {
            bool bVerbAllowsBody = cbxMethod.Text.OICEqualsAny("POST", "PUT", "PATCH", "QUERY", "DELETE", "OPTIONS");
            txtBody.BackColor = bVerbAllowsBody ? SystemColors.Window : SystemColors.ControlLight;
            // TODO: Go red for TRACE and CONNECT?
        }
    }
}
