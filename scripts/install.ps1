<#
.SYNOPSIS
    Silent, per-machine deployment for OrderBuddy Screen Manager.

.DESCRIPTION
    Simple replacement for a Burn bundle. Ensures the Microsoft Edge WebView2
    Evergreen Runtime is present (downloads and installs it silently if missing),
    then installs the MSI silently. Must be run elevated (per-machine install).

.PARAMETER MsiPath
    Path to ScreenManager.Installer.msi. Defaults to the Release build output.

.PARAMETER SkipWebView2
    Skip the WebView2 runtime check/install.

.EXAMPLE
    # From an elevated PowerShell prompt:
    powershell -ExecutionPolicy Bypass -File scripts\install.ps1
#>
param(
    [string]$MsiPath,
    [switch]$SkipWebView2
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    $MsiPath = Join-Path $repoRoot "src/ScreenManager.Packaging/ScreenManager.Installer/bin/x64/Release/ScreenManager.Installer.msi"
}

if (-not (Test-Path $MsiPath)) {
    throw "MSI not found at $MsiPath. Run scripts/package-release.ps1 first."
}

# Require elevation for a per-machine install.
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This installer must be run from an elevated (Administrator) PowerShell session."
}

function Test-WebView2Installed {
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    )
    foreach ($key in $keys) {
        $version = (Get-ItemProperty -Path $key -Name "pv" -ErrorAction SilentlyContinue).pv
        if (-not [string]::IsNullOrWhiteSpace($version) -and $version -ne "0.0.0.0") {
            return $true
        }
    }
    return $false
}

if (-not $SkipWebView2.IsPresent) {
    if (Test-WebView2Installed) {
        Write-Host "WebView2 Evergreen Runtime already present."
    }
    else {
        Write-Host "WebView2 Evergreen Runtime not found. Downloading bootstrapper..."
        $setup = Join-Path $env:TEMP "MicrosoftEdgeWebView2Setup.exe"
        Invoke-WebRequest -UseBasicParsing -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $setup
        Write-Host "Installing WebView2 Evergreen Runtime silently..."
        $wv = Start-Process -FilePath $setup -ArgumentList "/silent", "/install" -Wait -PassThru
        if ($wv.ExitCode -ne 0) {
            throw "WebView2 runtime install failed with exit code $($wv.ExitCode)."
        }
        Write-Host "WebView2 Evergreen Runtime installed."
    }
}

Write-Host "Installing OrderBuddy Screen Manager (silent, per-machine)..."
$install = Start-Process msiexec.exe -ArgumentList "/i", "`"$MsiPath`"", "/qn", "/norestart" -Wait -PassThru
if ($install.ExitCode -ne 0) {
    throw "MSI install failed with exit code $($install.ExitCode)."
}

Write-Host "Installation complete."
