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

// TODO: Add URLs to Wiki of Script Examples
// Many examples can be found within https://gist.github.com/ericlaw1979, but they will
// require light customization to work in Clearinet (e.g. replace mention of "fiddler" with
// "clearinet".

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
    // =============
    // Following are simple examples you can uncomment to try...
    // =============

    // Bind a column to the Exchanges listview.
    // See https://clearinet.app/r/?appcolumns for more info
    /*
      public static BindUIColumn("Method", 60)
      function FillMethodColumn(oEx: Exchange): String {
         return oEx.RequestMethod;
      }
      
      public BindUIColumn("OrigID", 50, //width 
      			   1,    // order
      			   true) // Sort numerically
	static function ShowOriginalID(oEx: oEx): String {
		// Note: In the SAZ format, the term "Exchange" 
		// is written as "Session"
		return oEx["x-OriginalSessionID"];
	}
    */
    
    // Add toolbar button for "Single Browser Mode", where only
    // one browser window will send its traffic to Clearinet.
    /*public static BindUIButton("SingleBrowserMode \uD83D\uDC40")
      function LaunchSingleInstance() {
      	// Tell the system we're not the proxy anymore.
        UI.actDetachProxy();
	// Launch a single browser instance pointed directly at ua.
	Utilities.LaunchNative('msedge.exe', 
	   '--user-data-dir="%temp%\\throwaway" --no-first-run --proxy-server=127.0.0.1:' + CONFIG.ListenPort.ToString() + " about:blank");
      }
    */

    // Add a "Links" menu to the main interface
    /*
    QuickLinkMenu("&Links") 
    QuickLinkItem("Clearinet on GitHub", "https://github.com/ericlaw1979/Clearinet")
    QuickLinkItem("Edge Team Demos", "https://github.com/MicrosoftEdge/Demos")
    public static function DoLinksMenu(sText: String, sAction: String)
    {
        Utilities.LaunchHyperlink(sAction);
    }
    */

    // Add a "Flags" tab to the main interface
    /*
       public BindUITab("Flags")
       static function FlagsReport(arrEx: Exchange[]):String {
        var sbOut: System.Text.StringBuilder = new System.Text.StringBuilder();
        for (var i:int = 0; i<arrEx.Length; ++i)
        {
            sbOut.AppendLine("FLAGS");
            sbOut.AppendFormat("{0}: {1}\n", arrEx[i].id, arrEx[i].fullUrl);
            for(var sFlag in arrEx[i].oFlags)
            {
                sbOut.AppendFormat("\t{0}:\t\t{1}\n", sFlag.Key, sFlag.Value);
            }
        }
        return sbOut.ToString();
    }
    
    public BindUITab("ReadTiming", true)  // TRUE here means "Show the content as HTML"  
    static function readsReport(arrEx: Exchange[]):String {
        var oSB: System.Text.StringBuilder = new System.Text.StringBuilder();
        oSB.Append("<html><head></head><body>");
        for (var i:int = 0; i<arrEx.Length; ++i)
        {
            var sClt:String = arrEx[i].Timers.ClientReads.ToString();
            var sSrv:String = arrEx[i].Timers.ServerReads.ToString();
            
            oSB.AppendFormat(
                "<h1>Exchange #{0}</h1><pre>{1}</pre><br /><br /><h2>Client Writes to Clearinet</h2><pre>{2}</pre><br /><h2>Clearinet Reads from Server</h2><pre>{3}</pre>", 
                arrEx[i].id, 
                Utilities.HtmlEncode(arrEx[i].fullUrl),
                sClt,
                sSrv
                ,""  // For reasons unexplored, .AddFormat with 4 values causes "more than one method or property matches this argument list". 
                // Likely regression in .NET4.6 due to new overload: https://msdn.microsoft.com/en-us/library/dn906225(v=vs.110).aspx
                );          
        }
        oSB.Append("</body></html>");
        return oSB.ToString();
    */

    public static RulesOption("&Auto-Authenticate")
    BindPref("cinscript.rules.AutoAuth")
    var m_AutoAuth: boolean = false;

    // Manually reload this script. This will resets all
    // RulesOption variables to their defaults.
    public static ToolsAction("Reset Script")
    function DoManualReload() { 
        AppObject.ReloadScript();
    }

    public static ContextAction("Decode Selected Exchanges")
    function DoRemoveEncoding(arrEx: Exchange[]) {
        for (var x:int = 0; x<arrEx.Length; ++x){
            arrEx[x].utilDecodeRequest();
            arrEx[x].utilDecodeResponse();
        }
        UI.actUpdateInspector(true,true);
    }
    
//
//
//  ============ HANDLERS FOR EXCHANGE PROCESSING EVENTS ============
//
//

    static function OnBeforeRequest(oEx: Exchange) {
        // Sample Rule: Color .ico requests in Blue
        // if (oEx.urlContains(".ico")) oEx["ui-color"] = "blue";

        // Sample Rule: Flag all POSTs to clearinet.app in italics
        // if (oEx.HostnameIs("clearinet.app") && oEx.HTTPMethodIs("POST")) { oEx["ui-italic"] = "yuppers"; }

        // Sample Rule: Breakpoint debugging. Pause requests for URLs containing "/sandbox/"
        // if (oEx.urlContains("/sandbox/")) {
        //     // Note: Existence of the x-breakrequest flag creates the breakpoint; its value is unimportant.
        //     oEx.oFlags["x-breakrequest"] = "script sez plz";
        // }

        if ((null != gs_ReplaceToken) && (oEx.url.indexOf(gs_ReplaceToken)>-1)) { // Replacement is case-sensitive
            oEx.url = oEx.url.Replace(gs_ReplaceToken, gs_ReplaceTokenWith); 
        }
        if ((null != gs_OverridenHost) && (oEx.host.toLowerCase() == gs_OverridenHost)) {
            oEx["x-overridehost"] = gs_OverrideHostWith; 
        }

        if ((null!=bpRequestURL) && oEx.urlContains(bpRequestURL)) {
            oEx["x-breakrequest"]="rules>bpu";
        }

        if ((null!=bpMethod) && (oEx.HTTPMethodIs(bpMethod))) {
            oEx["x-breakrequest"]="rules>bpm";
        }

        if ((null!=uiBoldURL) && oEx.urlContains(uiBoldURL)) {
            oEx["ui-bold"]="rules>bold";
        }

        if (m_AutoAuth) {
            // Respond to any HTTP authentication challenges using the 
            // current Windows user's credentials. You can change the 
            // token "(default)" to a domain\\username:password string 
            // if you like.
            //
            // Setting this flag on a remotely-sourced (non-loopback)
            // connection is a security risk because it allows "bouncing"
            // the request through your proxy and having it authenticate as you.
            if (oEx.ClientIsLoopback()) oEx["X-AutoAuth"] = "(default)";
        }

    }

    // OnPeekAtRequestHeaders is called just after a set of request headers has
    // been read from the client. This is often too early to do useful work
    // since oEx.requestBodyBytes hasn't yet been read. But if you want to hide
    // the Exchange or otherwise reduce processing on it, it's the ideal location.
/*
    static function OnPeekAtRequestHeaders(oEx: Exchange) {
        var sProc = ("" + oEx["x-ProcessInfo"]).ToLower();
        if (!sProc.StartsWith("mylowercaseappname")) {
        	oEx["ui-hide"] = "NotMyApp";
        	// TODO: Set the Ignore() flag to disable decryption and further 
        	// event handlers
        }
    }
*/

    //
    // This OnPeekAtResponseHeaders function is always called BEFORE the
    // response body is read from the server. That makes it a good place
    // to disable streaming if tampering with the response body is desired.
    //
    static function OnPeekAtResponseHeaders(oEx: Exchange) {
        // CApp.Log.LogFormat("Exchange {0}: Header peek status is {1}", oEx.id, oEx.responseCode);
        if ((bpStatus>0) && (oEx.responseCode == bpStatus)) {
            oEx["x-breakresponse"]="rules>bp";
            oEx.bBufferResponse = true;
        }
        
        if ((null!=bpResponseURL) && oEx.urlContains(bpResponseURL)) {
            oEx["x-breakresponse"]="rules>url";
            oEx.bBufferResponse = true;
        }
    }

    // If response Streaming is enabled, OnBeforeResponse fires AFTER the response
    // was returned to the client. In contrast, if you need to tamper with the
    // response, disable streaming inside OnPeekAtResponseHeaders.
    static function OnBeforeResponse(oEx: Exchange) {
        // if (isNotInteresting) oEx["ui-hide"] = "not interesting";
    }

/*
    // OnReturningError runs if Clearinet itself is returning an error (e.g.
    // "DNS lookup failed" to the client.) In these cases, OnBeforeResponse
    // does not run.
    static function OnReturningError(oEx: Exchange) {
    }
*/
//
//
//  ============ HANDLERS FOR APP EVENTS ============
//
//
/*
    // OnDone runs after an Exchange completes, regardless of outcome. It typically
    // fires AFTER the last regular update of the Exchange's UI item, so you must
    // manually refresh the UI if you intend to make an update.
    static function OnDone(oEx: Exchange) {
    }
*/

    /*
    static function OnBoot() {
        AppObject.alert("Clearinet has finished booting");
        System.Diagnostics.Process.Start("msedge.exe");

        UI.ActivateRequestInspector("HEADERS");
        UI.ActivateResponseInspector("HEADERS");
    }
    */
    /*
    static function OnBeforeShutdown(): Boolean {
        // Return false to cancel shutdown.
        return ((0 == UI.lvExchanges.TotalItemCount()) ||
                (DialogResult.Yes == MessageBox.Show("Allow Clearinet to exit?", "Close?",
                 MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)));
    }
    static function OnShutdown() {
            MessageBox.Show("Clearinet has shutdown");
    }
    */
    /*
    static function OnAttach() {
        MessageBox.Show("Clearinet is now the system proxy");
    }

    static function OnDetach() {
        MessageBox.Show("Clearinet is no longer the system proxy");
    }
    */

    // This function runs each time your script loads & compiles.
    static function Main() {
        var today: Date = new Date();
        AppObject.StatusText = " CustomRules.js loaded at: " + today;

        // Add column with the response's "Server" header
        // UI.lvExchanges.AddBoundColumn("Server", 50, "@response.server");

        // Add a global hotkey (Win+G) that invokes an ExecAction method named "screenshot"
        // UI.RegisterCustomHotkey(HotkeyModifiers.Windows, Keys.G, "screenshot"); 
    }

    // These static variables are used for simple breakpointing & other QuickExec rules 
    BindPref("cinscript.ephemeral.bpRequestURL")
    public static var bpRequestURL:String = null;

    BindPref("cinscript.ephemeral.bpResponseURL")
    public static var bpResponseURL:String = null;

    BindPref("cinscript.ephemeral.bpMethod")
    public static var bpMethod: String = null;

    static var bpStatus:int = -1;
    static var uiBoldURL: String = null;
    static var gs_ReplaceToken: String = null;
    static var gs_ReplaceTokenWith: String = null;
    static var gs_OverridenHost: String = null;
    static var gs_OverrideHostWith: String = null;

    // This function runs on commands from QuickExec or 
    // the ExecAction.exe command line utility.
    static function OnExecAction(sArgs: String[]): Boolean {

        AppObject.StatusText = "ExecAction: " + sArgs[0];

        var sAction = sArgs[0].toLowerCase();
        switch (sAction) {
        case "bold":
            if (2>sArgs.Length) {uiBoldURL=null; AppObject.StatusText="Bolding cleared"; return false;}
            uiBoldURL = sArgs[1]; AppObject.StatusText="Bolding requests for " + uiBoldURL;
            return true;
        case "bp":
            AppObject.alert("bpu = breakpoint request for url\nbpm = breakpoint request method\nbps=breakpoint response status\nbpafter = breakpoint response for url");
            return true;
        case "bps":
            if (2>sArgs.Length) {bpStatus=-1; AppObject.StatusText="Response Status breakpoint cleared"; return false;}
            bpStatus = parseInt(sArgs[1]); AppObject.StatusText="Response status breakpoint for " + sArgs[1];
            return true;
        case "bpv":
        case "bpm":
            if (2>sArgs.Length) {bpMethod=null; AppObject.StatusText="Request Method breakpoint cleared"; return false;}
            bpMethod = sArgs[1].toUpperCase(); AppObject.StatusText="Request Method breakpoint for " + bpMethod;
            return true;
        case "bpu":
            if (2>sArgs.Length) {bpRequestURL=null; AppObject.StatusText="RequestURL breakpoint cleared"; return false;}
            bpRequestURL = sArgs[1]; 
            AppObject.StatusText="RequestURL breakpoint for "+sArgs[1];
            return true;
        case "bpa":
        case "bpafter":
            if (2>sArgs.Length) {bpResponseURL=null; AppObject.StatusText="Cleared ResponseURL breakpoint"; return false;}
            bpResponseURL = sArgs[1]; 
            AppObject.StatusText="ResponseURL breakpoint for "+sArgs[1];
            return true;
        case "overridehost":
            if (3sArgs.Length) {gs_OverridenHost=null; AppObject.StatusText="Cleared Host Override"; return false;}
            gs_OverridenHost = sArgs[1].toLowerCase();
            gs_OverrideHostWith = sArgs[2];
            AppObject.StatusText="Connecting to [" + gs_OverrideHostWith + "] for requests to [" + gs_OverridenHost + "]";
            return true;
        case "urlreplace":
            if (3>sArgs.Length) {gs_ReplaceToken=null; AppObject.StatusText="Cleared URL Replacement"; return false;}
            gs_ReplaceToken = sArgs[1];
            gs_ReplaceTokenWith = sArgs[2].Replace(" ", "%20");
            AppObject.StatusText="Replacing [" + gs_ReplaceToken + "] in URLs with [" + gs_ReplaceTokenWith + "]";
            return true;
        case "allbut":
        case "keeponly":
            if (2>sArgs.Length) { AppObject.StatusText="Error: Specify Content-Type to retain."; return false;}
            UI.actSelectExchangesWithResponseHeaderValue("Content-Type", sArgs[1]);
            UI.actRemoveUnselectedExchanges();
            UI.lvExchanges.SelectedItems.Clear();
            AppObject.StatusText="Removed all except Content-Type: " + sArgs[1];
            return true;
        case "stop":
            UI.actDetachProxy();
            return true;
        case "start":
            UI.actAttachProxy();
            return true;
        case "clear":
        case "cls":
            UI.actRemoveAllExchanges();
            return true;
        case "g":
        case "go":
            UI.actResumeAllExchanges();
            return true;
        case "goto":
            if (sArgs.Length != 2) return false;
            Utilities.LaunchHyperlink("https://www.google.com/search?hl=en&btnI=I%27m+Feeling+Lucky&q=" + Utilities.UrlEncode(sArgs[1]));
            return true;
        case "help":
            Utilities.LaunchHyperlink("https://clearinet.app/r/?quickexec");
            return true;
        case "hide":
            UI.actMinimizeToTray();
            return true;
        case "log":
            AppObject.Log.LogString((2>sArgs.Length) ? "QuickExec logged empty string..." : sArgs[1]);
            return true;
        case "screenshot":
            // Capture a screenshot and store it in the Exchange list.
            UI.actCaptureScreenshot(false);
            return true;
        case "show":
            // Show the main window. (Useful from ExecAction.exe scripts)
            UI.actRestoreWindow();
            return true;
        case "tail":
            if (2>sArgs.Length) { AppObject.StatusText="Please specify # of exchanges to trim the list to."; return false;}
            UI.TrimExchangeList(int.Parse(sArgs[1]));
            return true;
        case "quit":
            UI.actExit();
            return true;
        case "dump":
            var sDumpFilename:String = CONFIG.GetPath("Captures") + "Clearinet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".saz";
            UI.actSelectAll();
            UI.actSaveExchangesToZip(sDumpFilename);
            AppObject.StatusText = "Stored all exchanges in " + sDumpFilename;
            UI.actRemoveAllExchanges();
            return true;

        default:
            if (sAction.StartsWith("http")) {
                AppObject.LaunchHyperlink(sArgs[0]);
                return true;
            }
      }  
      
      AppObject.StatusText = "Requested ExecAction: '" + sAction + "' doesn't exist. Type HELP to learn more.";
      return false;
    } // end of OnExecAction
}