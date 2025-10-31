@echo off
setlocal

REM === PROJECT SETTINGS ===
set DLL_PATH=C:\DEV\Project\VisualStudioC#\CTMOleClient\CTMOleClient\bin\x86\Debug\CTMOleClient.dll
set REGASM=C:\Windows\Microsoft.NET\Framework\v4.0.30319\regasm.exe

echo ============================================
echo REGISTER COMPONENT: %DLL_PATH%
echo ============================================

REM === CHECK IF DLL EXISTS ===
if not exist "%DLL_PATH%" (
    echo [ERROR] FILE NOT FOUND: %DLL_PATH%
    pause
    exit /b 1
)

REM === UNREGISTER OLD VERSION ===
echo.
echo [STEP 1] UNREGISTER OLD VERSION...
"%REGASM%" /unregister "%DLL_PATH%"
if errorlevel 1 (
    echo [WARNING] UNREGISTER MAY HAVE FAILED OR COMPONENT WAS NOT REGISTERED.
)

REM === REGISTER NEW VERSION ===
echo.
echo [STEP 2] REGISTER NEW VERSION...
"%REGASM%" /tlb /codebase "%DLL_PATH%"
if errorlevel 1 (
    echo [ERROR] FAILED TO REGISTER COMPONENT.
    pause
    exit /b 1
)

echo.
echo [DONE] COMPONENT SUCCESSFULLY REGISTERED.
echo.
echo.
echo.
echo.
endlocal
