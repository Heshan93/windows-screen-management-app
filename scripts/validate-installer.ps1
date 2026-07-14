param(
    [string]$Configuration = "Release",
    [switch]$UninstallAfterValidation
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$msiPath = Join-Path $repoRoot "src/ScreenManager.Packaging/ScreenManager.Installer/bin/x64/$Configuration/ScreenManager.Installer.msi"
$installedExe = Join-Path ${env:ProgramFiles} "OrderBuddy\ScreenManager\ScreenManager.App.exe"
$runKeyPath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run"

if (-not (Test-Path $msiPath)) {
    throw "MSI was not found at $msiPath. Run scripts/package-release.ps1 first."
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This validation must be run from an elevated (Administrator) PowerShell session (per-machine install)."
}

Write-Host "Running silent install (msiexec /i /qn)..."
$install = Start-Process msiexec.exe -ArgumentList "/i", "`"$msiPath`"", "/qn", "/norestart" -Wait -PassThru
if ($install.ExitCode -ne 0) {
    throw "MSI install failed with exit code $($install.ExitCode)."
}

if (-not (Test-Path $installedExe)) {
    throw "Installed app executable was not found at $installedExe."
}

$runValue = Get-ItemProperty -Path $runKeyPath -Name "OrderBuddy.ScreenManager.v2" -ErrorAction SilentlyContinue
if ($null -eq $runValue) {
    throw "Run key registration was not found under HKLM Run."
}

Write-Host "Validation checks passed: install exists and startup Run key is registered."

if ($UninstallAfterValidation.IsPresent) {
    Write-Host "Running silent uninstall (msiexec /x /qn)..."
    $uninstall = Start-Process msiexec.exe -ArgumentList "/x", "`"$msiPath`"", "/qn", "/norestart" -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) {
        throw "MSI uninstall failed with exit code $($uninstall.ExitCode)."
    }

    if (Test-Path $installedExe) {
        throw "Installed app executable still exists after uninstall: $installedExe"
    }

    $runValueAfterUninstall = Get-ItemProperty -Path $runKeyPath -Name "OrderBuddy.ScreenManager.v2" -ErrorAction SilentlyContinue
    if ($null -ne $runValueAfterUninstall) {
        throw "Run key still exists after uninstall."
    }

    Write-Host "Silent uninstall completed and cleanup checks passed."
}
