// TEMPORARY FILE for testing the script engine. 
// This file will be updated before v1.0. LAST UPDATED for v0.0.3.0
import System;
import System.Windows.Forms;
import Clearinet;

// Howdy! Please don't be scared by this script! :-)
//
// This is the Clearinet Rules file, which creates some of the menu commands and
// other features of the tool. You can edit this file to modify or add new commands.

// If you ever mess CustomRules.js up too badly to fix it, you can just delete the
// CustomRules file and the tool will automatically copy over the default SampleRules.js 
// file included with Clearinet.
//
// Eventually, we'll ship a custom editor for this file, but for now you can use Visual
// Studio Code or any similar tool. Ensure your tool saves this file using UTF-8 Encoding.

// Note that the default language used here is JScript.NET, which is SIMILAR to
// JavaScript, but there are some differences to be aware of. The samples below should
// help you understand them.

// Check out this handy page of examples and instructions: 
//     https://github.com/ericlaw1979/Clearinet/blob/main/Docs/SCRIPT_EXAMPLES.md

// Further examples can be found within https://gist.github.com/ericlaw1979, but they will
// require light customization to work in Clearinet; see the SCRIPT_EXAMPLES.md file
// for help.

// JScript.NET Reference:
// https://learn.microsoft.com/en-us/dotnet/api/microsoft.jscript?view=netframework-4.8.1

// =============================
// The Handlers class here holds pretty much everything you 
// will ever add to this file. Do not rename it, or the app
// will not call your code.
// =============================
class Handlers 
{
//
// ============ EXTEND THE APP UI ============
// By annotating a method with a BindUIColumn attribute you can
// add a new column to the Exchanges list view. By adding a BindUITab
// attribute you can add a new top-level tab to the App UI. By adding a
// BindUIButton, you can add a button to the toolbar. By adding a QuickLinks
// menu, you can add a new item to the top-level menu.
//
    static var sMyInfo: String;

    // This function runs on every compile of the script.
    static function Main() {
        var today: Date = new Date();
        sMyInfo = today.toString();
        // if (!CONFIG.isViewerMode) CApp.PlaySound("C:\\Windows\\Media\\notify.wav");
        // if (!CONFIG.isViewerMode) CApp.alert("Hello from script!\n\n" + sMyInfo+"\n"+CApp.VersionString);
        var sKey = "Win+Shift+G";
        CApp.UI.SetStatusText("Binding '" + sKey + "' to 'paste' returned: " + CApp.UI.bindHotkeyToExecAction(sKey, "paste"));
        CApp.UI.SetStatusText("Your script has compiled and loaded!");
        CApp.UI.lvExchanges.AddBoundColumn("Server", 50, "@response.Server");   // Add a Response Header column.
        CApp.UI.lvExchanges.AddBoundColumn("Accept", 50, "@request.Accept");    // Add a Request Header column.
        CApp.UI.lvExchanges.AddBoundColumn("ui-backcolor", 50, "ui-backcolor"); // Add an Exchange Flag column.
    }
    
    // This function runs when the script is unloaded.
    static function OnRetire() {
        CApp.Log.Log("! The script '" + sMyInfo + "' is retiring at " + new Date());
        CApp.UI.lvExchanges.RemoveBoundColumn("Server");
        CApp.UI.lvExchanges.RemoveBoundColumn("Accept");
        CApp.UI.lvExchanges.RemoveBoundColumn("ui-backcolor");
    }

    // The BindUIButton attribute makes this a toggle on the toolbar.
    BindUIButton("AllowClose", "Allow Clearinet to close without blocking.")
    BindPref("demoscript.ephemeral.allow_close")
    public static var bCanClose: boolean = true;

    RulesOption("Some Random Toggler&2")
    BindPref("demoscript.randomToggle#2")
    public static var bRandomToggler2: boolean;

    RulesOption("A Persisted String...")
    BindPref("demoscript.user_persisted_string")
    public static var sString: String = "default value from script";

    BindUIButton("SingleBrowserMode \uD83D\uDC40", "Launches a browser pointed at the Clearinet proxy.")
    public static function LaunchSingleInstance() {
        Utilities.LaunchNative('msedge.exe', '--user-data-dir="C:\\temp\\throwaway" --no-first-run --proxy-server=127.0.0.1:8888 about:blank');
    }

    public static BindTextCodec("to UPPERCASE")
    function codecToUpper(input: String): String {
        return input.ToUpper();
    }
     
    BindUIColumn("Content-Encoding", false)
    public static function FillCTColumn(xc: Exchange): String {
        if (xc.bHasResponse) return xc.ResponseHeaders["Content-Encoding"];
        return String.Empty;
    }

    // Use Numeric sorting
    BindUIColumn("RequestBody", true)
    public static function FillSizeColumn(xc: Exchange): String {
        if (null == xc.RequestBody) return 0;
        return xc.RequestBody.Length.ToString("N0");
    }

    // Add a "Links" top-level menu to the main window.
    QuickLinkMenu("&Links") 
    QuickLinkItem("&Clearinet on GitHub", "https://github.com/ericlaw1979/Clearinet")
    QuickLinkItem("Edge Team's &Demos", "https://github.com/MicrosoftEdge/Demos")
    QuickLinkItem("Eric's &Tests", "https://debugtheweb.com/test/")
    public static function DoLinksMenu(sText: String, sAction: String)
    {
        Utilities.LaunchHyperlink(sAction);
    }
    
    BindUITab("Flags", "<fixedwidth><nowrap><nolink><hidden>")
    public static function FlagsReport(arrXC: Exchange[]): String {
        var sbOut: System.Text.StringBuilder = new System.Text.StringBuilder();
        for (var i: int = 0; i < arrXC.Length; ++i) {
            sbOut.AppendLine("FLAGS");
            sbOut.AppendFormat("Exchange Flags #{0} (for '{1}')\n", arrXC[i].id, arrXC[i].fullUrl);
            for (var sFlag in arrXC[i].oFlags)
                sbOut.AppendFormat("\t{0}:\t\t{1}\n", sFlag.Key, sFlag.Value);

            sbOut.AppendFormat("\n BitFlags: {0}\n\n", arrXC[i].BitFlags);

            if (arrXC[i].ResponseHeaders.Exists("Content-Encoding")) {
                var strDecoded = arrXC[i].GetResponseBodyAsString();
                sbOut.AppendLine("== Content was compressed.========== \nDecoded content is " + strDecoded.Length.ToString() + " bytes.\n");
                sbOut.AppendLine("Decoded content: " + strDecoded.slice(0, 2048));
            }
        }
        return sbOut.ToString();
    }

    ToolsAction("Decode Selected Exchanges")
    ContextAction("Decode Exchanges")
    public static function DoRemoveEncoding(arrXC: Exchange[]) {
        for (var x: int = 0; x < arrXC.Length; ++x) {
            arrXC[x].utilDecodeRequest();
            arrXC[x].utilDecodeResponse();
        }
        CApp.UI.actUpdateInspector(true, true);
        CApp.UI.SetStatusText("Decoded " + x.ToString() + " Exchanges.");
    }

    ToolsAction("Reset Script")
    public static function DoManualReload() {
        CApp.UI.actFlashWindow();
        CApp.scriptRules.ReloadScript();
    }

    static function OnBoot() {
        CApp.Log.Log("[Script] OnBoot");
    }

    static function OnAttach() {
        CApp.Log.Log("[Script] OnAttach");
    }

    static function OnDetach() {
        CApp.Log.Log("[Script] OnDetach");
    }

    static function OnShutdown() {
        CApp.Log.Log("[Script] OnShutdown");
        // if (!CONFIG.isViewerMode) CApp.alert("Bye! Thanks for playing\n" + sMyInfo);
    }

    static function OnBeforeShutdown(): boolean {
        if (!bCanClose) CApp.alert("Close is disallowed. QuickExec 'sesame' to unlock");
        return bCanClose;
    }

    // ================================================================================
    // Functions called during Exchange processing...
    // ================================================================================
    static function OnBeforeRequest(XC: Exchange) {
        // XC["x-replywithfile"] = "C:\\temp\\foo.html";
        XC["ui-backcolor"] = "LimeGreen";
    }

    static function OnBeforeResponse(XC: Exchange) {
        XC["ui-backcolor"] = "#FFF5DD";
    }

    // ================================================================================
    static function OnExecAction(sArgs: String[]): Boolean {
        var sAction = sArgs[0].toLowerCase();
        switch (sAction) {
            case "clear":
            case "cls":
                CApp.UI.actRemoveAllExchanges();
                return true;

            case "start":
                CApp.actAttachProxy();
                return true;
            case "stop":
                CApp.actDetachProxy();
                return true;
        
            case "find":
                if (sArgs.length > 1) CApp.UI.actBeginFinding(sArgs[1]); else CApp.UI.actBeginFinding();
                break;

            case "keeponly":
                if (2 > sArgs.Length) { CApp.UI.SetStatusText("Error: Specify Content-Type to retain."); return false;}

                CApp.UI.actSelectExchanges(
                    function (xc: Exchange): boolean {
                        return xc.MediaType.Contains(sArgs[1].ToLower());
                    },
                    Int32.MaxValue,
                    true);

                CApp.UI.actRemoveUnselectedExchanges();
                CApp.UI.lvExchanges.SelectedItems.Clear();
                CApp.UI.SetStatusText("Removed all except Content-Type: " + sArgs[1]);
            return true;
        
            case "hide":
                CApp.UI.actMinimizeToTray();
            return true;

            case "screenshot":
                // Capture a screenshot and store it in the Exchange list.
                CApp.UI.actCaptureScreenshot(false);
                return true;

            case "show":
                // Show the main window. (Useful from ExecAction.exe scripts)
                CApp.UI.actRestoreWindow();
                return true;

            case "quit":
                CApp.UI.actExit();
                return true;

            case "dump":
                var sDumpFilename:String = CONFIG.GetPath("Captures") + "Clearinet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".saz";
                CApp.UI.actSaveAllExchanges(sDumpFilename);
                CApp.UI.SetStatusText("Stored all Exchanges in " + sDumpFilename);
                CApp.UI.actRemoveAllExchanges();
            return true;

            case "stayalive":
                CApp.UI.SetStatusText("disallow close");
                bCanClose = false;
                return;

            case "sesame":
                CApp.UI.SetStatusText("Now allowing close");
                bCanClose = true;
                return;

            case "ver":
                CApp.alert(CApp.VersionString);
                return true;

            case "paste":
                if (CApp.UI.actPasteFromClipboard()) {
                    CApp.UI.actFlashWindow();
                    CApp.PlaySound("C:\\Windows\\Media\\Windows Print complete.wav")
                }
                break;

            case "log":
                CApp.Log.Log(Array(sArgs).slice(1).join(" "));
                return true;

            default:
                if (sAction.StartsWith("http")) {
                    Utilities.LaunchHyperlink(sArgs[0]);
                    return true;
    		    }
        }
        CApp.UI.SetStatusText("Unknown execaction: " + sAction);
        CApp.alert("OnExecAction '" + sAction + "' was called with " + (sArgs.Length - 1).toString() + " arguments.");
        return false;
    } // end of OnExecAction
}