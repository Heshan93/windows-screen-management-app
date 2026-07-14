param(
    [string]$Configuration = "Release",
    [string]$CertificateThumbprint = "",
    [string]$TimeStampUrl = "http://timestamp.digicert.com",
    [string]$BuildRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

# When -BuildRoot is supplied (e.g. a drive with more free space), redirect the large
# publish output and all temp/CAB work there so a nearly-full C: drive does not break the build.
$publishArgs = @()
if (-not [string]::IsNullOrWhiteSpace($BuildRoot)) {
    $publishDir = (Join-Path $BuildRoot "publish\win-x64") + "\"
    $tempDir = Join-Path $BuildRoot "temp"
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    $env:TEMP = $tempDir
    $env:TMP = $tempDir
    $publishArgs = @("-p:PublishDir=$publishDir")
    Write-Host "BuildRoot override active. Publish output: $publishDir ; TEMP: $tempDir"
}

try {
    Write-Host "Publishing ScreenManager.App (win-x64 self-contained single-file)..."
    dotnet publish src/ScreenManager.App/ScreenManager.App.csproj -c $Configuration -p:PublishProfile=win-x64-selfcontained @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE. The MSI was NOT rebuilt. Fix the publish error before packaging."
    }

    Write-Host "Building MSI package..."
    dotnet build src/ScreenManager.Packaging/ScreenManager.Installer/ScreenManager.Installer.csproj -c $Configuration @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSI build failed with exit code $LASTEXITCODE."
    }

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
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE."
        }
    }
    else {
        Write-Warning "CertificateThumbprint is empty. MSI was built but not signed."
    }

    # Assemble a ready-to-ship deployment folder the customer can copy and run.
    $deployDir = Join-Path $repoRoot "artifacts/deploy"
    if (Test-Path $deployDir) {
        Remove-Item $deployDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

    Copy-Item $msiPath (Join-Path $deployDir "ScreenManager.Installer.msi") -Force
    Copy-Item (Join-Path $repoRoot "scripts/Install-OrderBuddy.bat") (Join-Path $deployDir "Install-OrderBuddy.bat") -Force
    Copy-Item (Join-Path $repoRoot "scripts/install.ps1") (Join-Path $deployDir "install.ps1") -Force
    if (Test-Path (Join-Path $repoRoot "RUNBOOK.md")) {
        Copy-Item (Join-Path $repoRoot "RUNBOOK.md") (Join-Path $deployDir "RUNBOOK.md") -Force
    }

    Write-Host "Packaging complete."
    Write-Host "MSI: $msiPath"
    Write-Host ""
    Write-Host "READY-TO-SHIP FOLDER: $deployDir"
    Write-Host "Copy this whole folder to the target PC and double-click 'Install-OrderBuddy.bat'."
}
finally {
    Pop-Location
}
