using System;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class frmInspect : Form
    {
        private Exchange XC;
        public frmInspect(Exchange xc)
        {
            XC = xc;
            InitializeComponent();
            rtbProperties.BackColor = CONFIG.colorDisabledEdit;
            rtbProperties.Text = Summarize();
            frmViewer.doPopulateInspectorTabs(tabsInspectRequest, tabsInspectResponse, "NewWindow");
            this.Text = $"{Text}{XC.id}";
        }

        private string Summarize()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ID: {XC.id} {XC.fullUrl}");
            sb.AppendLine($"State: {XC.state}");
            sb.AppendLine();
            sb.AppendLine($"-- FLAGS -------------------");
            sb.AppendLine($"BitFlags: [{XC.BitFlags}]");
            foreach (var flag in XC.oFlags)
            {
                sb.AppendLine($"   {flag.Key}: {flag.Value}");
            }
            sb.AppendLine();
            sb.AppendLine($"-- TIMERS ------------------");
            sb.Append(XC.Timers.ToString(true));
            DateTime dtFirst = XC.Timers.ClientBeginRequest;
            if ((dtFirst.Ticks > 0) && (dtFirst.Date != DateTime.Today))
            {
                sb.Append($"\n\nThis Exchange was captured on {dtFirst.Date:D}.");
            }

            return sb.ToString();
        }

        private void UpdateInspectors(bool bRequest, bool bResponse)
        {
            if (CApp.isClosing) return;
            if (bRequest && tabsInspectRequest.SelectedIndex > -1)
            {
                var page = tabsInspectRequest.TabPages[tabsInspectRequest.SelectedIndex];
                RequestInspectorBase ibRequest = page.Tag as RequestInspectorBase;
                ibRequest.EnsureTabReady(page);
                ibRequest?.AssignExchange(XC);
            }
            if (bResponse && tabsInspectResponse.SelectedIndex > -1)
            {
                var page = tabsInspectResponse.TabPages[tabsInspectResponse.SelectedIndex];
                ResponseInspectorBase ibResponse = page.Tag as ResponseInspectorBase;
                ibResponse.EnsureTabReady(page);
                ibResponse?.AssignExchange(XC);
            }
        }
        private void tabsInspectRequest_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            UpdateInspectors(true, false);
        }

        private void tabsInspectResponse_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            UpdateInspectors(false, true);
        }

        private void tabsInspect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabsInspect.SelectedTab == pageRequest)
            {
                UpdateInspectors(true, false);
            }
            else if (tabsInspect.SelectedTab == pageResponse)
            {
                UpdateInspectors(false, true);
            }
        }
    }
}
