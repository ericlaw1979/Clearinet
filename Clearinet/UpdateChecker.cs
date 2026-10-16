using Clearinet;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class UpdateChecker
{
    public static async Task CheckForUpdatesAsync(bool bAlwaysShowOutcome = false)
    {
        LongRunningOperation lro = null;
        if (bAlwaysShowOutcome)
        {
            lro = new LongRunningOperation("Checking for updates...", false);
        }

        CApp.Log.LogFormat($"Checking for new version...");

        // All service traffic from Clearinet is gated by the `app.service_traffic` preference.
        // If sBigSwitchState is "prompt", then directly confirm with the user if the version check
        // was requested by the user. If it was an automated version check, simply skip it.
        var sBigSwitchState = CApp.Prefs.GetStringPref("app.service_traffic", "allowed");
        if (!"allowed".OICEquals(sBigSwitchState))
        {
            if (!bAlwaysShowOutcome)
            {
                CApp.Log.LogFormat($"Version check disallowed because `app.service_traffic` is not set to `allowed`.");
                return;
            }
            if ("prompt".OICEquals(sBigSwitchState))
            {
                var result = MessageBox.Show(
                    CApp.UI, "Can we make a webservice call to request update information?",
                    "SERVICE CALL PERMISSION",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes) return;
            }
            else
            {
                CApp.UIInvokeAsync(new Action(() =>
                {
                    MessageBox.Show(
                        CApp.UI, "Version check disallowed because `app.service_traffic` is not set to `allowed`",
                        "Update Check Disabled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }));
            }
        }

        try
        {
            using (HttpClient client = new HttpClient())
            {
                Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"Clearinet/{currentVersion}");
                client.Timeout = TimeSpan.FromSeconds(10);

                var sApiUrl = CApp.Prefs.GetStringPref("app.updater.api_url", "https://api.github.com/repos/ericlaw1979/clearinet/releases/latest");
                string jsonResponse = await client.GetStringAsync(sApiUrl).ConfigureAwait(false);

                Match tagMatch = Regex.Match(jsonResponse, @"\""tag_name\""\s*:\s*\""v?([0-9\.]+)\""", RegexOptions.IgnoreCase);
                Match urlMatch = Regex.Match(jsonResponse, @"\""html_url\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);

                if (!tagMatch.Success || !Version.TryParse(tagMatch.Groups[1].Value, out Version latestVersion))
                {
                    throw new System.IO.InvalidDataException("The response from the update server did not contain a valid version tag.");
                }

                if (latestVersion > currentVersion)
                {
                    string releaseUrl = urlMatch.Success ? urlMatch.Groups[1].Value : "https://github.com/ericlaw1979/clearinet/releases";
                    CApp.Log.Log($"Clearinet is up-to-date. Current Version: {currentVersion}, Latest Version: {latestVersion}");

                    // Marshal back using the target form if provided
                    CApp.UIInvokeAsync(new Action(() => PromptUserToUpdate(currentVersion, latestVersion, releaseUrl)));
                }
                else
                {
                    CApp.Log.Log($"You are running the latest version of Clearinet. Current Version: {currentVersion}, Latest Version: {latestVersion}");

                    if (bAlwaysShowOutcome && !CONFIG.isQuietMode)
                    {
                        CApp.UIInvokeAsync(new Action(() =>
                        {
                            MessageBox.Show(
                                CApp.UI,
                                $"You are running the latest version of Clearinet.\n\nCurrent Version: {currentVersion}\nLatest Version:  {latestVersion}",
                                "No Updates Available",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            CApp.Log.LogFormat($"Update check failed:{ex.Message}");
            if (bAlwaysShowOutcome)
            {
                if (ex is TaskCanceledException)
                {
                    CApp.UIInvokeAsync(new Action(() =>
                    {
                        MessageBox.Show(
                            CApp.UI,
                            "The update check timed out. Please check your network connection or try again later.",
                            "Update Check Timed Out",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }));
                    lro?.Dispose();
                    return;
                }

                CApp.ReportException(ex, "Update check failed.");
            }
        }

        lro?.Dispose();
    }

    private static void PromptUserToUpdate(Version currentVersion, Version latestVersion, string releaseUrl)
    {
        string message = $"A new version of Clearinet is available!\n\n" +
                         $"Current Version:\t{currentVersion}\n" +
                         $"Latest Version:\t{latestVersion}\n\n";
        if (CONFIG.isQuietMode)
        {
            CApp.Log.Log(message);
            return;
        }
        else
        {
            message += $"Would you like to open the download page?";
        }

        DialogResult result = MessageBox.Show(
            CApp.UI,
            message,
            "Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            Utilities.LaunchHyperlink(releaseUrl);
        }
    }
}