param(
    [string]$Configuration = "Release",
    [string]$CertificateThumbprint = "",
    [string]$TimeStampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    Write-Host "Publishing ScreenManager.App (win-x64 self-contained single-file)..."
    dotnet publish src/ScreenManager.App/ScreenManager.App.csproj -c $Configuration -p:PublishProfile=win-x64-selfcontained

    Write-Host "Building MSI package..."
    dotnet build src/ScreenManager.Packaging/ScreenManager.Installer/ScreenManager.Installer.csproj -c $Configuration

    $msiPath = Join-Path $repoRoot "src/ScreenManager.Packaging/ScreenManager.Installer/bin/x64/$Configuration/ScreenManager.Installer.msi"

    if (-not (Test-Path $msiPath)) {
        throw "MSI was not found at $msiPath."
    }

    if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        $signTool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
        if ([string]::IsNullOrWhiteSpace($signTool)) {
            throw "signtool.exe is not available. Install Windows SDK Signing Tools or run signing in CI with signtool available."
        }

        Write-Host "Signing $msiPath"
        & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /td SHA256 /tr $TimeStampUrl "$msiPath"
    }
    else {
        Write-Warning "CertificateThumbprint is empty. MSI was built but not signed."
    }

    Write-Host "Packaging complete."
    Write-Host "MSI: $msiPath"
    Write-Host "Deploy with: scripts/install.ps1 (installs WebView2 runtime if missing, then the MSI)."
}
finally {
    Pop-Location
}
