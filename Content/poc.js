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
        CApp.PlaySound("C:\\Windows\\Media\\notify.wav");
        CApp.alert(sMyInfo+"\n"+CApp.VersionString);
        CApp.UI.SetStatusText("Your script has compiled and loaded!");
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
      CApp.alert("Bye! Thanks for playing\n" + sMyInfo);
    }
    
    static function OnBeforeShutdown(): boolean {
      return bCanClose;
    }
        
     static function OnExecAction(sArgs: String[]): Boolean {
       var sAction = sArgs[0].toLowerCase();
        switch (sAction) {
        case "find":
           if (sArgs.length>1) frmFind.BeginFinding(sArgs[1]); else frmFind.BeginFinding();
           break;
        case "seasame":
        MessageBox.Show("allowing close");
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