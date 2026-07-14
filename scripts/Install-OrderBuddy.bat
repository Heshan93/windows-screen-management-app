@echo off
setlocal
title OrderBuddy Screen Manager - Installer

rem ============================================================
rem  One-click installer for OrderBuddy Screen Manager.
rem  Double-click this file. It will ask for Administrator
rem  permission (UAC), install the Microsoft Edge WebView2
rem  runtime if missing, install the app, and launch it.
rem ============================================================

rem --- Self-elevate to Administrator if not already elevated ---
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator permission...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo ============================================================
echo   Installing OrderBuddy Screen Manager
echo ============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
set "RC=%errorlevel%"

echo.
if "%RC%"=="0" (
    echo ============================================================
    echo   Installation complete.
    echo   The setup wizard will appear on the screen.
    echo   To configure later: press Ctrl+Shift+F12, PIN 1234.
    echo ============================================================
) else (
    echo ============================================================
    echo   Installation FAILED. See the messages above.
    echo ============================================================
)

echo.
pause
endlocal
