# Legacy Inno Setup packaging helper. Primary release flow now uses Velopack.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-ProjectVersion {
    [xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
    $version = $props.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Version not found in Directory.Build.props"
    }
    return $version
}

$version = Get-ProjectVersion

Write-Host "Step 1: Publishing app..." -ForegroundColor Cyan
dotnet publish "GameTrainerLauncher.UI\GameTrainerLauncher.UI.csproj" -p:PublishProfile=FolderProfile -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Write-Host "Inno Setup 6 not found at: $iscc" -ForegroundColor Yellow
    Write-Host "Publish completed. Open Installer\GameTrainerLauncher.iss in Inno Setup and compile manually." -ForegroundColor Yellow
    exit 0
}

Write-Host "Step 2: Compiling installer..." -ForegroundColor Cyan
& $iscc "/DMyAppVersion=$version" "Installer\GameTrainerLauncher.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Installer: Installer\Output\GameTrainerLauncher_Setup_$version.exe" -ForegroundColor Green
