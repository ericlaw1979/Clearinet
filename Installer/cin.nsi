; This file contains the Nullsoft Installer script
; for installing Clearinet. You only need to run this
; script if you intend to build a setup.exe app to
; install the program.
;
; To build, run:
;  <folder>\nsis\makensis.exe cin.nsi
;
; For release builds, you must first sign the binaries
; inside the package, then build the installer, then
; sign the installer.
;
; Get NSIS from https://nsis.sourceforge.io/Download
; Read the docs for the install script here:
;    https://nsis.sourceforge.io/Docs/Chapter3.html
;
; Last validated against NSIS 3.12
; 
Name "Clearinet"
;Icon "..\media\setup.ico"
ManifestDPIAware true

!include x64.nsh
!include WinVer.nsh
!include "FileFunc.nsh"

; Directives to the installer
XPStyle on
SetCompressor /solid lzma
OutFile "ClearinetSetup.exe"

; Definitions used in the script
!define /file VER_APP Clearinet.ver
!define /date NOW "%b-%d-%y"

; TODO: Allow user level installations. For now, the installer is machine-wide
RequestExecutionLevel "admin"

LicenseText "Agreement to the license is required to install."
LicenseData "..\docs\EULA.txt"

BrandingText "v${VER_APP} (${NOW})" 
VIProductVersion "${VER_APP}"
VIAddVersionKey "FileVersion" "${VER_APP}"
VIAddVersionKey "ProductName" "Clearinet Installer"
VIAddVersionKey "Comments" "https://clearinet.app/"
VIAddVersionKey "LegalCopyright" "©2026 Clearinet Contributors"
VIAddVersionKey "CompanyName" "Clearinet Contributors"
VIAddVersionKey "FileDescription" "Installs Clearinet Web Debugger"

InstallDir "$PROGRAMFILES\Clearinet"
InstallDirRegKey HKLM "SOFTWARE\Clearinet" "InstallPath"
DirText "Select installation folder:"

Section "App"

; Install for all users by default
SetShellVarContext all
SetOutPath "$INSTDIR"

; TODO: Check for .NET binaries
; TODO: Check for running version

DetailPrint "Installing Clearinet"
ClearErrors
SetOverwrite on

;File "..\app\bin\Release\Cin.exe"		; TODO: Need the app
;File "..\app\bin\Release\Cin.exe.config"	; TODO: Need the config
File "..\Docs\Credits.txt"
; File "..\Media\SazFile.ico"			; TODO: Create icon


; Install any 3P dependencies
; Install any default extensions
; Install any template scripts/responses/etc

IfErrors +1 lbl_UpdateRegistry
IfSilent FailWithCode

MessageBox MB_OK "Error: Writing a file failed.$\nAn admin must install this tool."
goto lbl_UpdateRegistry

FailWithCode:
SetErrorLevel 100
goto lbl_WriteUninstall

lbl_UpdateRegistry:
ClearErrors

DetailPrint "Writing Win32 Registry"
WriteRegStr HKLM "SOFTWARE\Clearinet" "InstallPath" "$INSTDIR\"
WriteRegStr HKLM "Software\Clearinet" "Version" "${VER_App}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\App Paths\cin.exe" "" '"$INSTDIR\cin.exe"'

; take SAZ File Association
WriteRegStr HKCR ".saz" "" "Clearinet.ArchiveZip"
WriteRegStr HKCR "Clearinet.ArchiveZip\DefaultIcon" "" "$INSTDIR\SazFile.ico"
WriteRegStr HKCR "Clearinet.ArchiveZip" "" "Clearinet Session Archive"
WriteRegStr HKCR "Clearinet.ArchiveZip\Shell\Open\command" "" '"$INSTDIR\cin.exe" -noattach "%1"'
WriteRegStr HKCR "Clearinet.ArchiveZip\Shell\Open V&iewer Mode\command" "" '"$INSTDIR\cin.exe" -viewer "%1"'
; TODO: Do we want to use the legacy MIME here?
WriteRegStr HKCR "Clearinet.ArchiveZip" "Content Type" "application/x-zip-compressed+SessionArchive"

DetailPrint "Writing Win64 Registry"
SetRegView 64 
WriteRegStr HKLM "SOFTWARE\Clearinet" "InstallPath" "$INSTDIR\"
WriteRegStr HKLM "Software\Clearinet" "Version" "${VER_App}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\App Paths\cin.exe" "" '"$INSTDIR\cin.exe"'

SetRegView lastused

IfErrors +1 RegistryOK
IfSilent +2 +1
MessageBox MB_OK "Warning: Registry update failed.$\nAn admin must install this tool."
SetErrorLevel 101
goto lbl_WriteUninstall

RegistryOk:

CreateShortCut "$SMPROGRAMS\Clearinet.lnk" "$INSTDIR\cin.exe" "" "$INSTDIR\cin.exe" 0

; TODO: Firewall exception

; TODO: NGEN for better boot performance

lbl_WriteUninstall:
WriteUninstaller "uninst.exe"


; Add/Remove Programs
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayName" "Clearinet"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "Publisher" "OSS Contributors"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "Comments" "Clearinet is a web debugger."
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "InstallLocation" "$INSTDIR"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayVersion" "${VER_APP}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "UninstallString" '"$INSTDIR\uninst.exe"'
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "DisplayIcon" '"$INSTDIR\cin.exe"'
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "HelpLink" "https://clearinet.app/r/?clnethelp"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "URLUpdateInfo" "http://clearinet.app/r/?clnetupdates"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "URLInfoAbout" "http://clearinet.app/"
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "NoRepair" 1
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "NoModify" 1

; Get size of install on disk
${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
IntFmt $0 "0x%08X" $0
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet" "EstimatedSize" "$0"

IfSilent lbl_Finish
ExecShell "open" "http://clearinet.app/r/?clnetinstalledorupdated" "" SW_SHOWNORMAL

lbl_Finish:

SectionEnd

Function .onInstFailed
SetErrorLevel 99
FunctionEnd

; ===================================
UninstallText "This program will uninstall the Clearinet Debugger."
; ===================================
Section Uninstall

IfSilent SkipWipe

; TODO: Ensure not running
MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Delete all settings?$\n$\n(Choose NO if you intend to install later)" IDNO SkipWipe
DetailPrint "Removing files and registry keys..."

; Remove HKCU
DeleteRegKey HKCU "SOFTWARE\Clearinet"
; TODO: Delete per user stuff

SkipWipe:

SetShellVarContext all

; Remove Start Menu shortcuts and IE Toolbar button
Delete "$SMPROGRAMS\Clearinet.lnk"

; Wipe registry HKLM keys
DeleteRegKey HKLM "SOFTWARE\Clearinet"
SetRegView 64 
DeleteRegKey HKLM "SOFTWARE\Clearinet" 
SetRegView lastused

DeleteRegKey HKCR ".saz" 
DeleteRegKey HKCR "Clearinet.ArchiveZip"
DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\App Paths\cin.exe"

; Remove files

; todo: Remove any NGEN assemblies

Delete "$INSTDIR\cin.exe"
Delete "$INSTDIR\cin.exe.config"
Delete "$INSTDIR\SazFile.ico"

; Remove uninstaller
DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Clearinet"
Delete "$INSTDIR\uninst.exe"

RMDir "$INSTDIR"

lbl_UninstallFinished:

SectionEnd