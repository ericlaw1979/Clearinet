// TEMPORARY FILE for testing the script engine. 
// This file will not exist in the first usable build
import System;
import System.Windows.Forms;
import Clearinet;

class Handlers {
    static var bCanClose: boolean = true; // set to false to play with close cancelation
    static var sMyInfo: String;
    static function Main() {
        var today: Date = new Date();
        sMyInfo = today.toString();
      if (!CONFIG.isViewerMode) CApp.PlaySound("C:\\Windows\\Media\\notify.wav");
        if (!CONFIG.isViewerMode) CApp.alert(sMyInfo+"\n"+CApp.VersionString);
        CApp.UI.SetStatusText("Your script has compiled and loaded!");
    }

    public static BindUIButton("SingleBrowserMode \uD83D\uDC40")
    function LaunchSingleInstance() {
      Utilities.LaunchNative('msedge.exe', '--user-data-dir="C:\\temp\\throwaway" --no-first-run --proxy-server=127.0.0.1:8888 about:blank');
    }
      
    public BindUITab("Flags")
    static function FlagsReport(arrEx: Exchange[]):String {
      var sbOut: System.Text.StringBuilder = new System.Text.StringBuilder();
      for (var i:int = 0; i<arrEx.Length; ++i)
      {
        sbOut.AppendLine("FLAGS");
        sbOut.AppendFormat("Exchange Flags #{0} (for '{1}')\n", arrEx[i].id, arrEx[i].fullUrl);
        for(var sFlag in arrEx[i].oFlags)
            sbOut.AppendFormat("\t{0}:\t\t{1}\n", sFlag.Key, sFlag.Value);
        
        sbOut.AppendFormat("\n BitFlags: {0}", arrEx[i].BitFlags);
      }
      return sbOut.ToString();
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
      if (!CONFIG.isViewerMode) CApp.alert("Bye! Thanks for playing\n" + sMyInfo);
    }
    
    static function OnBeforeShutdown(): boolean {
      if (!bCanClose) CApp.alert("Close is disallowed. QuickExec 'sesame' to unlock");
      return bCanClose;
    }
        
     static function OnExecAction(sArgs: String[]): Boolean {
       var sAction = sArgs[0].toLowerCase();
        switch (sAction) {
        case "find":
           if (sArgs.length>1) frmFind.BeginFinding(sArgs[1]); else frmFind.BeginFinding();
           break;
           
        case "stayalive":
        CApp.UI.SetStatusText("disallow close");
        bCanClose = false;
        return;
        
        case "sesame":
        CApp.UI.SetStatusText("Now allowing close");
        bCanClose = true;
        return;
        
        case "ver":
        MessageBox.Show(CApp.VersionString);
        return true;
        
        case "log":
        CApp.Log.Log(Array(sArgs).slice(1).join(" "));
        return true;
        
        default:
          CApp.UI.SetStatusText("Unknown execaction: " + sAction);
          MessageBox.Show("OnExecAction '"+ sAction +"' was called with " + (sArgs.Length-1).toString() + " arguments.", "Script");
          return false;
        }
     }

}