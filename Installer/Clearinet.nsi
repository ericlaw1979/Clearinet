; This file contains the Nullsoft Installer script
; for installing Clearinet. You only need to run this
; script if you intend to build a setup.exe app to
; install the program.
;
; To build, run:
;   <folder>\nsis\makensis.exe Clearinet.nsi
;
; For release builds, you must first sign the binaries
; inside the package, then build the installer, then
; sign the installer.
;
; Get NSIS from https://nsis.sourceforge.io/Download
; Read the docs for the install script here:
;      https://nsis.sourceforge.io/Docs/Chapter3.html
;
; Last validated against NSIS 3.13
;  
Name "Clearinet"
!define MUI_ICON "..\Content\Builtin\setup.ico"
ManifestDPIAware true

!include x64.nsh
!include WinVer.nsh
!include "FileFunc.nsh"
!include "MUI2.nsh"

; Define MultiUser settings before including MultiUser.nsh
!define MULTIUSER_EXECUTIONLEVEL Highest	; Note: An admin cannot just decline to elevate and have setup still run
!define MULTIUSER_MEMBERPAGE_DISPLAY_OPTION
; --- Default to Per-Machine (All Users) Install Mode ---
!define MULTIUSER_INSTALLMODE_DEFAULT_ALLUSERS
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "Software\Clearinet"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallPath"
!define MULTIUSER_INSTALLMODE_INSTDIR "Clearinet"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "Software\Clearinet"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallPath"

!include "MultiUser.nsh"

; Directives to the installer
SetCompressor /solid lzma
OutFile "ClearinetSetup.exe"

; Definitions used in the script
!define /file VER_APP Clearinet.ver
!define /date NOW "%b-%d-%y"


; ========================================================================
!include "LogicLib.nsh"

; Check for running windows by title (shared by installer and uninstaller)
Function CheckRunningInstances
  loop_check:
    FindWindow $0 "" "Clearinet.ExecActionTarget"
    ${If} $0 != 0
      Goto prompt_user
    ${EndIf}

    FindWindow $0 "" "Clearinet Viewer"
    ${If} $0 != 0
      Goto prompt_user
    ${EndIf}

    Goto done

  prompt_user:
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION \
      "Clearinet is currently running.$\r$\n$\r$\nPlease close all instances of Clearinet before continuing." \
      IDRETRY loop_check
    Abort

  done:
FunctionEnd

; Uninstaller variant for CheckRunningInstances
Function un.CheckRunningInstances
  loop_check:
    FindWindow $0 "" "Clearinet.ExecActionTarget"
    ${If} $0 != 0
      Goto prompt_user
    ${EndIf}

    FindWindow $0 "" "Clearinet Viewer"
    ${If} $0 != 0
      Goto prompt_user
    ${EndIf}

    Goto done

  prompt_user:
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION \
      "Clearinet is currently running.$\r$\n$\r$\nPlease close all instances of Clearinet before continuing." \
      IDRETRY loop_check
    Abort

  done:
FunctionEnd

; .NET Framework 4.8.1 Minimum Release Key
!define NET481_RELEASE_BUILD 533320
!define NET481_DOWNLOAD_URL "https://go.microsoft.com/fwlink/?LinkId=2203304"

Function CheckAndInstallDotNet481
    DetailPrint "Checking for .NET Framework 4.8.1..."
    
    Push $0
    
    ; Save current registry view and inspect the 32-bit setup key
    SetRegView 32
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    SetRegView lastused

    ${If} $0 >= ${NET481_RELEASE_BUILD}
        DetailPrint ".NET Framework 4.8.1 is already installed ($0)."
        Pop $0
        Return
    ${EndIf}

    ; Prompt user to download .NET 4.8.1 manually without stopping setup
    MessageBox MB_YESNO|MB_ICONEXCLAMATION \
        "Clearinet requires .NET Framework 4.8.1 or higher, which does not appear to be installed.$\n$\nWould you like to open your browser to download it now?$\nYou will need to install it before continuing." \
        IDNO continue_setup

    ExecShell "open" "${NET481_DOWNLOAD_URL}"

continue_setup:
    DetailPrint "Continuing installation..."
    Pop $0
FunctionEnd
# ========================================================================

; Setup Installer Pages
!insertmacro MUI_PAGE_LICENSE "..\docs\EULA.txt"
!define MUI_TEXT_LICENSE_SUBTITLE "Accept the friendly license to install and use Clearinet."
!define MUI_INNERTEXT_LICENSE_BOTTOM "If you accept, click I Agree to continue."
!define MUI_INNERTEXT_LICENSE_TOP " " ; remove the instructions about how to Page Down. we're not your mom.

; Because we're building a 32-bit setup.exe to install a 64-bit app, the MutiUser.nsh
; template incorrectly assumes we want to install to 32bit Program Files, and the usual
; override mechanism doesn't work correctly. So we need to wait for the last moment and
; then update the $INSTDIR value ourselves.
;
!define MULTIUSER_PAGE_CUSTOMFUNCTION_LEAVE SetTargetDirByInstallMode
Function SetTargetDirByInstallMode
  ${If} $MultiUser.InstallMode == "AllUsers"
    StrCpy $INSTDIR "$PROGRAMFILES64\Clearinet"
  ${EndIf}
FunctionEnd

!insertmacro MULTIUSER_PAGE_INSTALLMODE  ; <--- Prompts user for CurrentUser vs AllUsers
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; --- Finish Page with Launch Option ---
; Use the "explorer.exe target.exe" hack to ensure that an elevated installer launches the app unelevated
!define MUI_FINISHPAGE_RUN "$WINDIR\explorer.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "$\"$INSTDIR\Clearinet.exe$\""
!define MUI_FINISHPAGE_RUN_TEXT "Launch Clearinet now"

!define MUI_FINISHPAGE_SHOWREADME "https://clearinet.app/r/?clnetinstalledorupdated"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Launch Release Notes url"

!insertmacro MUI_PAGE_FINISH

; Setup Uninstaller Pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Language files must be inserted AFTER page macros
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  # Check for existing window instances before continuing
  Call CheckRunningInstances

  # Initialize MultiUser mode detection
  !insertmacro MULTIUSER_INIT

  # Verify 64-bit OS at startup
  ${If} ${IsNativeAMD64}
  ${OrIf} ${IsNativeARM64}
    # Proceed normally on 64-bit OS
  ${Else}
    MessageBox MB_YESNO|MB_ICONEXCLAMATION "Clearinet's installer currently only works on 64-bit Windows.$\r$\n$\r$\nInstall anyway?" IDYES +2
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  # Check for existing window instances before continuing uninstallation
  Call un.CheckRunningInstances

  !insertmacro MULTIUSER_UNINIT
FunctionEnd

BrandingText "v${VER_APP} (${NOW})" 
VIProductVersion "${VER_APP}"
VIAddVersionKey "FileVersion" "${VER_APP}"
VIAddVersionKey "ProductName" "Clearinet Installer"
VIAddVersionKey "Comments" "https://clearinet.app/"
VIAddVersionKey "LegalCopyright" "©2026 Clearinet Contributors"
VIAddVersionKey "CompanyName" "Clearinet Contributors"
VIAddVersionKey "FileDescription" "Installs Clearinet Web Debugger"

Section "App"

SetOutPath "$INSTDIR"

; Ensure no running instances remain when file copy begins
Call CheckRunningInstances

; Check for .NET Framework
Call CheckAndInstallDotNet481

DetailPrint "Installing Clearinet"
ClearErrors
SetOverwrite on

File "..\Clearinet\bin\Release\Clearinet.exe"
File "..\Clearinet\bin\Release\Clearinet.exe.config"

; Building Clearinet copies the SampleRules file from the original in \Content\Bundled\ to the Release folder.
File "..\Clearinet\bin\Release\SampleRulesAlpha.js"    

File "..\Docs\Credits.txt"
File "..\Content\SAZFile.ico"

; Install any 3P dependencies
File "..\Clearinet\bin\Release\Ionic.Zip.Reduced.dll"
File "..\Clearinet\bin\Release\ZstdSharp.dll"
File "..\Clearinet\bin\Release\BrotliSharpLib.dll"

File "..\Clearinet\bin\Release\CBOR.dll"
File "..\Clearinet\bin\Release\Numbers.dll"
File "..\Clearinet\bin\Release\URIUtility.dll"

File "..\Extensions\bin\Release\SVG.dll"
File "..\Extensions\bin\Release\ExCss.dll"
; We've absorbed this code directly into the Extensions assembly. If they
; later take my patch, then we'll put this back.
;File "..\Extensions\bin\Release\be.windows.forms.hexbox.dll"
  
; Microsoft Support Assemblies Zstd and Brotli rely upon:
File "..\Clearinet\bin\Release\System.Buffers.dll"
File "..\Clearinet\bin\Release\System.Memory.dll"
File "..\Clearinet\bin\Release\System.Numerics.Vectors.dll"
File "..\Clearinet\bin\Release\System.Runtime.CompilerServices.Unsafe.dll"
File "..\Clearinet\bin\Release\System.Threading.Tasks.Extensions.dll"

; Install default extensions

SetOutPath "$INSTDIR\Extensions\"
File "..\Extensions\bin\Release\CAE-Default.dll"
File "..\Extensions\bin\Release\Microsoft.Web.WebView2.Core.dll"
File "..\Extensions\bin\Release\Microsoft.Web.WebView2.WinForms.dll"

;SetOutPath "$INSTDIR\Extensions\runtimes\win-x86\native"
;File "..\Extensions\bin\Release\runtimes\win-x86\native\WebView2Loader.dll"
SetOutPath "$INSTDIR\Extensions\runtimes\win-x64\native"
File "..\Extensions\bin\Release\runtimes\win-x64\native\WebView2Loader.dll"
SetOutPath "$INSTDIR\Extensions\runtimes\win-arm64\native"
File "..\Extensions\bin\Release\runtimes\win-arm64\native\WebView2Loader.dll"

SetOutPath "$INSTDIR\ImportExport\"
File "..\Transcoders\bin\Release\CAT-Default.dll"

SetOutPath "$INSTDIR\Tools\"
File "..\Content\bundled\dwebp.exe"
File "..\Content\bundled\CallExecAction.ps1"

; Install any template scripts/responses/etc

IfErrors +1 lbl_UpdateRegistry
IfSilent FailWithCode

MessageBox MB_OK "Error: Writing a file failed."
goto lbl_UpdateRegistry

FailWithCode:
SetErrorLevel 100
goto lbl_WriteUninstall

lbl_UpdateRegistry:
ClearErrors

; TODO: Should we need to use the 64-bit Registry View ???
; SetRegView 64

DetailPrint "Writing Registry"
WriteRegStr SHCTX "Software\Clearinet" "InstallPath" "$INSTDIR\"
WriteRegStr SHCTX "Software\Clearinet" "Version" "${VER_App}"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\App Paths\clearinet.exe" "" '"$INSTDIR\clearinet.exe"'

; Tell the system what we support
WriteRegStr SHCTX "Software\Classes\Applications\Clearinet.exe\SupportedTypes" ".har" ""
WriteRegStr SHCTX "Software\Classes\Applications\Clearinet.exe\SupportedTypes" ".saz" ""

; SAZ File Association
WriteRegStr SHCTX "Software\Classes\.saz" "" "Clearinet.ArchiveZip"
WriteRegStr SHCTX "Software\Classes\Clearinet.ArchiveZip\DefaultIcon" "" "$INSTDIR\SAZFile.ico"
WriteRegStr SHCTX "Software\Classes\Clearinet.ArchiveZip" "" "Clearinet Session Archive"
WriteRegStr SHCTX "Software\Classes\Clearinet.ArchiveZip\Shell\Open\command" "" '"$INSTDIR\clearinet.exe" -noattach "%1"'
WriteRegStr SHCTX "Software\Classes\Clearinet.ArchiveZip\Shell\Open V&iewer Mode\command" "" '"$INSTDIR\clearinet.exe" -viewer "%1"'
; TODO: Do we want to use the legacy MIME here?
WriteRegStr SHCTX "Software\Classes\Clearinet.ArchiveZip" "Content Type" "application/x-zip-compressed+SessionArchive"

WriteRegStr SHCTX "Software\Classes\Clearinet.HARFile" "" "HTTP Archive File"
WriteRegStr SHCTX "Software\Classes\Clearinet.HARFile" "FriendlyTypeName" "HTTP Archive File"
# Associated Icon (using main executable icon at index 0)
WriteRegStr SHCTX "Software\Classes\Clearinet.HARFile\DefaultIcon" "" "$INSTDIR\Clearinet.exe,0"
# Default "open" verb shell execution command
WriteRegStr SHCTX "Software\Classes\Clearinet.HARFile\shell\open\command" "" '"$INSTDIR\Clearinet.exe" "%1"'
WriteRegStr SHCTX "Software\Classes\.har\OpenWithProgids" "Clearinet.HARFile" ""
WriteRegStr SHCTX "Software\Classes\Applications\Clearinet.exe\shell\open\command" "" '"$INSTDIR\Clearinet.exe" "%1"'

# Tell Windows Explorer to refresh associated file icons
System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, i 0, i 0)'

DetailPrint "Writing Win64 Registry"
SetRegView 64 
WriteRegStr SHCTX "Software\Clearinet" "InstallPath" "$INSTDIR\"
WriteRegStr SHCTX "Software\Clearinet" "Version" "${VER_App}"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\App Paths\clearinet.exe" "" '"$INSTDIR\clearinet.exe"'

SetRegView lastused

IfErrors +1 RegistryOK
IfSilent +2 +1
MessageBox MB_OK "Warning: Registry update failed."
SetErrorLevel 101
goto lbl_WriteUninstall

RegistryOk:

CreateShortCut "$SMPROGRAMS\Clearinet.lnk" "$INSTDIR\clearinet.exe" "" "$INSTDIR\clearinet.exe" 0

; Add Firewall Exception (Admin / AllUsers mode only)
${If} $MultiUser.InstallMode == "AllUsers"
    DetailPrint "Adding Windows Firewall exception..."
    nsExec::ExecToStack 'netsh advfirewall firewall add rule name="Clearinet" dir=in action=allow program="$INSTDIR\Clearinet.exe" enable=yes profile=any description="Allow inbound traffic for Clearinet Web Debugger"'
${EndIf}

; NGEN for better boot performance
DetailPrint "Optimizing assembly performance (NGEN)..."
IfFileExists "$WINDIR\Microsoft.NET\Framework64\v4.0.30319\ngen.exe" 0 lbl_SkipNGEN
    nsExec::ExecToStack '"$WINDIR\Microsoft.NET\Framework64\v4.0.30319\ngen.exe" install "$INSTDIR\Clearinet.exe" /queue'
lbl_SkipNGEN:

lbl_WriteUninstall:
WriteUninstaller "uninst.exe"

; Add/Remove Programs (Context-sensitive)
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayName" "Clearinet"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "Publisher" "OSS Contributors"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "Comments" "Clearinet is a web debugger."
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "InstallLocation" "$INSTDIR"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayVersion" "${VER_APP}"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "UninstallString" '"$INSTDIR\uninst.exe"'
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayIcon" '"$INSTDIR\clearinet.exe"'
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "HelpLink" "https://clearinet.app/r/?clnethelp"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "URLUpdateInfo" "http://clearinet.app/r/?clnetupdates"
WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "URLInfoAbout" "http://clearinet.app/"
WriteRegDWORD SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "NoRepair" 1
WriteRegDWORD SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "NoModify" 1

; Get size of install on disk
${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
IntFmt $0 "0x%08X" $0
WriteRegDWORD SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "EstimatedSize" "$0"

lbl_Finish:

SectionEnd

Function .onInstFailed
SetErrorLevel 99
FunctionEnd

; ===================================
Section Uninstall

Call un.CheckRunningInstances

IfSilent SkipWipe

MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Delete all settings?$\n$\n(Choose NO if you intend to install later)" IDNO SkipWipe
DetailPrint "Removing files and registry keys..."

SkipWipe:

; Remove Start Menu shortcuts
Delete "$SMPROGRAMS\Clearinet.lnk"

; Remove Firewall Rule
nsExec::ExecToStack 'netsh advfirewall firewall delete rule name="Clearinet" program="$INSTDIR\Clearinet.exe"'

; Uninstall NGEN native images
IfFileExists "$WINDIR\Microsoft.NET\Framework64\v4.0.30319\ngen.exe" 0 lbl_SkipUnNGEN
    nsExec::ExecToStack '"$WINDIR\Microsoft.NET\Framework64\v4.0.30319\ngen.exe" uninstall "$INSTDIR\Clearinet.exe"'
lbl_SkipUnNGEN:

; Wipe registry keys from current context
DeleteRegKey SHCTX "Software\Clearinet"
SetRegView 64 
DeleteRegKey SHCTX "Software\Clearinet" 
SetRegView lastused

DeleteRegKey SHCTX "Software\Classes\.saz" 
DeleteRegKey SHCTX "Software\Classes\Clearinet.ArchiveZip"
DeleteRegKey SHCTX "Software\Microsoft\Windows\CurrentVersion\App Paths\clearinet.exe"

# Remove ProgID association from .har extension
DeleteRegValue SHCTX "Software\Classes\.har\OpenWithProgids" "Clearinet.HARFile"

# Delete the ProgID registry tree
DeleteRegKey SHCTX "Software\Classes\Clearinet.HARFile"
DeleteRegKey SHCTX "Software\Classes\Applications\Clearinet.exe"

# Refresh shell cache
System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, i 0, i 0)'

; Remove files
Delete "$INSTDIR\clearinet.exe"
Delete "$INSTDIR\clearinet.exe.config"
Delete "$INSTDIR\Credits.txt"
Delete "$INSTDIR\SAZFile.ico"
Delete "$INSTDIR\SampleRulesAlpha.js"
Delete "$INSTDIR\Ionic.Zip.Reduced.dll"
Delete "$INSTDIR\ZstdSharp.dll"
Delete "$INSTDIR\BrotliSharpLib.dll"
Delete "$INSTDIR\System.Buffers.dll"
Delete "$INSTDIR\SVG.dll"
Delete "$INSTDIR\ExCSS.dll"
Delete "$INSTDIR\be.windows.forms.hexbox.dll"
Delete "$INSTDIR\System.Memory.dll"
Delete "$INSTDIR\System.Numerics.Vectors.dll"
Delete "$INSTDIR\System.Runtime.CompilerServices.Unsafe.dll"
Delete "$INSTDIR\System.Threading.Tasks.Extensions.dll"


Delete "$INSTDIR\Tools\dwebp.exe"
Delete "$INSTDIR\Tools\CallExecAction.ps1"

Delete "$INSTDIR\Extensions\CAE-Default.dll"
Delete "$INSTDIR\Extensions\Microsoft.Web.WebView2.Core.dll"
Delete "$INSTDIR\Extensions\Microsoft.Web.WebView2.WinForms.dll"
;Delete "$INSTDIR\Extensions\runtimes\win-x86\native\WebView2Loader.dll"
Delete "$INSTDIR\Extensions\runtimes\win-x64\native\WebView2Loader.dll"
Delete "$INSTDIR\Extensions\runtimes\win-arm64\native\WebView2Loader.dll"
Delete "$INSTDIR\ImportExport\CAT-Default.dll"
Delete "$INSTDIR\CBOR.dll"
Delete "$INSTDIR\Numbers.dll"
Delete "$INSTDIR\URIUtility.dll"
Delete "$INSTDIR\URIUtility.dll"

; Remove directories iff empty
;RMDir "$INSTDIR\Extensions\runtimes\win-x86\native"
;RMDir "$INSTDIR\Extensions\runtimes\win-x86"
RMDir "$INSTDIR\Extensions\runtimes\win-x64\native"
RMDir "$INSTDIR\Extensions\runtimes\win-x64"
RMDir "$INSTDIR\Extensions\runtimes\win-arm64\native"
RMDir "$INSTDIR\Extensions\runtimes\win-arm64"
RMDir "$INSTDIR\Extensions\runtimes"
RMDir "$INSTDIR\Tools"
RMDir "$INSTDIR\Extensions"
RMDir "$INSTDIR\ImportExport"

; Remove uninstaller and Uninstall key
DeleteRegKey SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet"
Delete "$INSTDIR\uninst.exe"

; TODO: We can specify RMDir /r /REBOOTOK $INSTDIR to wipe all files even if the folder isn't empty.
; ... but doing that is potentially dangerous and we should prompt first?
RMDir "$INSTDIR"

lbl_UninstallFinished:

SectionEnd