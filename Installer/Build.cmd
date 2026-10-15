@title Clearinet Builder
@powershell -Command "(Get-Item '..\Clearinet\bin\release\clearinet.exe').VersionInfo.FileVersion | Set-Content 'Clearinet.ver'"
@pause
@:signing
@REM // TODO: For post-quantum security, we want to use SHA384 hashes, but those aren't supported until Win11 or Win10 with a Nov2022 Update.
@REM //       Standard practice for this scenario would be to dual-sign the files. But since we're still using RSA here, the stronger hash
@REM //       seems like it would be of questionable value?
@"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /as /d "Clearinet Web Debugger" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\Clearinet\bin\release\Clearinet.exe 
@"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /as /d "Clearinet Extensions" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\Extensions\bin\release\CAE-Default.dll 
@"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /as /d "Clearinet Transcoders" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 ..\Transcoders\bin\release\CAT-Default.dll 
@"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /as /d "Clearinet Transcoders" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 C:\src\NSIS\Plugins\x86-unicode\UserInfo.dll"
@echo Build installer
@if %ERRORLEVEL%==-1 goto singing
@cd c:\src\cin\installer\
@c:\src\nsis\MakeNSIS.EXE Clearinet.nsi
@if %ERRORLEVEL%==1 goto done
@:signsetup
@"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" sign /as /d "Clearinet Web Debugger" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 ClearinetSetup.exe 

@if %ERRORLEVEL%==-1 goto signsetup
@pause
@:done
@title Command Prompt