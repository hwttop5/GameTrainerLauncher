# 一键生成安装包：先 publish，再调用 Inno Setup 编译（若已安装）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

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
& $iscc "Installer\GameTrainerLauncher.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Installer: Installer\Output\GameTrainerLauncher_Setup_1.0.0.exe" -ForegroundColor Green
