@title Clearinet Builder
@filever c:\src\cin\bin\release\cin.exe > cin.ver
@pushd c:\src\cin\bin\release
@pause
@:signing
@echo Sign Clearinet
@signtool sign /as /d "Clearinet Web Debugger" /du "https://clearinet.app/" /n "Eric Lawrence" /t http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\bin\release\cin.exe 
@echo Sign utilities
@signtool sign /as /d "Clearinet Updater" /du "https://clearinet.app/" /n "Eric Lawrence" /t http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\Updater\bin\release\UpdateClearinet.exe 
@signtool sign /as /d "Clearinet Certificate Trust" /du "https://clearinet.app/" /n "Eric Lawrence" /t http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\TrustCert\bin\release\TrustCert.exe 
@signtool sign /as /d "EnableLoopback for Win AppContainers" /du "https://clearinet.app/" /n "Eric Lawrence" /t http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\EnableLoopback\bin\release\EnableLoopback.exe

@echo Build installer
@if %ERRORLEVEL%==-1 goto singing
@cd c:\src\cin\installer\
@c:\src\nsis\MakeNSIS.EXE /DSRCPATH=Release /V2 clearinet.nsi
@if %ERRORLEVEL%==1 goto done
@:signsetup
@CHOICE /M "Sign setup.exe?"
@if %ERRORLEVEL%==2 goto done
@signtool sign /as /d "Clearinet Web Debugger" /du "https://clearinet.app/" /n "Eric Lawrence" /t http://timestamp.digicert.com /td SHA256 /fd SHA256 ClearinetSetup.exe 

@if %ERRORLEVEL%==-1 goto signsetup
@pause
@:done
@title Command Prompt