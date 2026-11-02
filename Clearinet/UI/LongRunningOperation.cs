using System;
using System.ComponentModel;
using System.Windows.Forms;
using static Clearinet.PlatformAPI;

namespace Clearinet
{
    public class LongRunningOperation : IDisposable
    {
        DateTime dtFirstProgress = DateTime.MinValue;
        private bool PreventSleepOrExit { get; set; }

        public string OperationName { get; private set; }

        public string StatusText { get; private set; }

        private ToolStripProgressBar ProgressUI;

        private readonly CancelEventHandler ceh;

        public LongRunningOperation(string sOperationName, bool bPreventSleepOrExit)
        {
            OperationName = sOperationName;
            PreventSleepOrExit = bPreventSleepOrExit;
            if (PreventSleepOrExit)
            {
                ceh = (s, e) =>
                {
                    CApp.UI.SetStatusText($"Please wait for the operation ('{OperationName}') to complete before exiting.");
                    e.Cancel = true;
                };
                //  Stopwatch oSW = Stopwatch.StartNew();
                PlatformAPI.PreventSleep(keepDisplayOn: false);
                //  Trace.WriteLine($"LongRunningOperation calling PreventSleep took {oSW.ElapsedMilliseconds}ms.");

                CApp.UI.OnWindowsQueryShutdown += BlockWinShutdown;
                CApp.BeforeAppShutdown += ceh;
            }
            CreateUI();
        }

        private void BlockWinShutdown(object sender, CancelEventArgs cea)
        {
            // Indicate to the OS that the application is blocking shutdown.
            ShutdownBlocker.BlockShutdown(CApp.UI.Handle, $"Please wait for: '{OperationName}' to complete.");
            cea.Cancel = true;
        }

        private void CreateUI()
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { CreateUI(); }));
                return;
            }
            ProgressUI = new ToolStripProgressBar()
            {
                Maximum = 100,
                ToolTipText = OperationName,
                Style = ProgressBarStyle.Marquee
            };

            CApp.UI.tsToolbar.Items.Insert(0, ProgressUI);
            Application.DoEvents();
        }

        public void UpdateProgress(int iPercent, string sStatusText = null)
        {
            if (dtFirstProgress == DateTime.MinValue) dtFirstProgress = DateTime.Now;
            if (sStatusText.HasText()) StatusText = sStatusText;
            if (iPercent >= 100)
            {
                iPercent = 100;
                //Dispose();
                return;
            }
            if (null != ProgressUI)
            {
                UpdateUIForProgress(iPercent);
            }
        }

        private void UpdateUIForProgress(int iPercent)
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { UpdateUIForProgress(iPercent); }));
                return;
            }
            if (null == ProgressUI) return;

            ProgressUI.Style = ProgressBarStyle.Blocks;
            ProgressUI.Value = iPercent;
            ProgressUI.ToolTipText = $"{OperationName}: {StatusText} ({iPercent:N0}%)";
            Application.DoEvents();
        }

        public void Dispose()
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { Dispose(); }));
                return;
            }
            CApp.Log.Log($"Long-running operation '{OperationName}' completed.");
            if (dtFirstProgress != DateTime.MinValue && DateTime.Now - dtFirstProgress > TimeSpan.FromSeconds(5))
            {
                CApp.UI.actFlashWindow();
            }
            CApp.UI.tsToolbar.Items.Remove(ProgressUI);
            ProgressUI.Dispose(); ProgressUI = null;
            if (PreventSleepOrExit)
            {
                PlatformAPI.RestoreSleep();
                CApp.UI.OnWindowsQueryShutdown -= BlockWinShutdown;
                CApp.BeforeAppShutdown -= ceh;
            }
        }

    }
}
