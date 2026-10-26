@echo off
setlocal enableextensions enabledelayedexpansion

rem Define path to SignTool
set "SIGNTOOL=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

rem Array of target files
set FILES[0]="..\Clearinet\bin\Release\Ionic.Zip.Reduced.dll"
set FILES[1]="..\Clearinet\bin\Release\ZstdSharp.dll"
set FILES[2]="..\Clearinet\bin\Release\BrotliSharpLib.dll"
set FILES[3]="..\Clearinet\bin\Release\CBOR.dll"
set FILES[4]="..\Clearinet\bin\Release\Numbers.dll"
set FILES[5]="..\Clearinet\bin\Release\URIUtility.dll"
set FILES[6]="..\Extensions\bin\Release\SVG.dll"
set FILES[7]="..\Extensions\bin\Release\ExCss.dll"
set FILES[8]="..\Content\bundled\dwebp.exe"
rem set FILES[9]="..\Extensions\bin\Release\be.windows.forms.hexbox.dll"

rem Loop through each file and sign
for /L %%I in (0,1,8) do (
    echo.
    echo ========================================================
    echo Signing: !FILES[%%I]!
    echo ========================================================
    
    "%SIGNTOOL%" sign /d "Clearinet Dependency" /du "https://clearinet.app/" /n "Eric Lawrence" /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 !FILES[%%I]!
    
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to sign !FILES[%%I]!
    ) else (
        echo [SUCCESS] Signed !FILES[%%I]!
    )
)

echo.
echo ========================================================
echo Signing complete.
echo ========================================================
pause