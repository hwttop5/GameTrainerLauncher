param(
    [string]$Channel = "win",
    [string]$ReleaseNotesPath = "",
    [string]$RepoUrl = "",
    [switch]$DownloadPreviousReleases
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-BuildProps {
    [xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
    $group = $props.Project.PropertyGroup
    return [PSCustomObject]@{
        Version = [string]$group.Version
        RepositoryUrl = [string]$group.RepositoryUrl
        PublishRuntime = [string]$group.PublishRuntime
    }
}

function Get-ReleaseNotesFile([string]$notesPath, [string]$version) {
    if (-not [string]::IsNullOrWhiteSpace($notesPath)) {
        return (Resolve-Path $notesPath).Path
    }

    $generatedPath = Join-Path $root "artifacts\release-notes.md"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $generatedPath) | Out-Null
    $content = @(
        "# Game Trainer Launcher v$version",
        "",
        "- Automatic update support via Velopack.",
        "- Packaging output for GitHub Releases."
    ) -join [Environment]::NewLine
    Set-Content -Path $generatedPath -Value $content -Encoding UTF8
    return $generatedPath
}

function Assert-SelfContainedRuntimeConfig([string]$runtimeConfigPath, [string]$hostFxrPath) {
    if (-not (Test-Path $runtimeConfigPath)) {
        throw "Published runtime config not found: $runtimeConfigPath"
    }
    if (-not (Test-Path $hostFxrPath)) {
        throw "Self-contained runtime file not found: $hostFxrPath"
    }

    $runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    if (-not $runtimeConfig.runtimeOptions.includedFrameworks) {
        throw "Published runtime config is not self-contained."
    }
    if ($runtimeConfig.runtimeOptions.frameworks) {
        throw "Published runtime config still contains framework references."
    }
}

function Assert-PackagedRuntimeConfig([string]$packagePath) {
    if (-not (Test-Path $packagePath)) {
        throw "Velopack full package not found: $packagePath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $extractDir = Join-Path $env:TEMP ("velopack-validate-" + [Guid]::NewGuid().ToString("N"))
    [System.IO.Compression.ZipFile]::ExtractToDirectory($packagePath, $extractDir)

    try {
        $runtimeConfigPath = Join-Path $extractDir "lib/app/GameTrainerLauncher.UI.runtimeconfig.json"
        $hostFxrPath = Join-Path $extractDir "lib/app/hostfxr.dll"
        Assert-SelfContainedRuntimeConfig -runtimeConfigPath $runtimeConfigPath -hostFxrPath $hostFxrPath
    }
    finally {
        if (Test-Path $extractDir) {
            Remove-Item -Recurse -Force $extractDir
        }
    }
}

$buildProps = Get-BuildProps
if ([string]::IsNullOrWhiteSpace($buildProps.Version)) {
    throw "Version not found in Directory.Build.props"
}

if ([string]::IsNullOrWhiteSpace($RepoUrl)) {
    $RepoUrl = $buildProps.RepositoryUrl
}

$publishDir = Join-Path $root "artifacts\publish"
$releaseDir = Join-Path $root "artifacts\velopack"
$notesFile = Get-ReleaseNotesFile -notesPath $ReleaseNotesPath -version $buildProps.Version

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Get-ChildItem -Path $releaseDir -Filter "*$($buildProps.Version)*" -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Restoring local tools..." -ForegroundColor Cyan
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing application..." -ForegroundColor Cyan
dotnet publish "GameTrainerLauncher.UI\GameTrainerLauncher.UI.csproj" `
    -p:PublishProfile=FolderProfile `
    -c Release `
    -r $buildProps.PublishRuntime `
    --self-contained true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$runtimeConfigPath = Join-Path $publishDir "GameTrainerLauncher.UI.runtimeconfig.json"
$hostFxrPath = Join-Path $publishDir "hostfxr.dll"
Assert-SelfContainedRuntimeConfig -runtimeConfigPath $runtimeConfigPath -hostFxrPath $hostFxrPath

if ($DownloadPreviousReleases -and -not [string]::IsNullOrWhiteSpace($RepoUrl)) {
    $token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } else { $env:GH_TOKEN }
    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Host "Skipping previous release download because GITHUB_TOKEN/GH_TOKEN is not set." -ForegroundColor Yellow
    }
    else {
        Write-Host "Downloading previous releases for delta generation..." -ForegroundColor Cyan
        dotnet tool run vpk download github `
            --repoUrl $RepoUrl `
            --channel $Channel `
            --outputDir $releaseDir `
            --token $token
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

Write-Host "Packing Velopack release..." -ForegroundColor Cyan
dotnet tool run vpk pack `
    --packId "GameTrainerLauncher" `
    --packVersion $buildProps.Version `
    --packDir $publishDir `
    --mainExe "GameTrainerLauncher.UI.exe" `
    --packTitle "Game Trainer Launcher" `
    --packAuthors "hwttop5" `
    --releaseNotes $notesFile `
    --channel $Channel `
    --runtime $buildProps.PublishRuntime `
    --icon "GameTrainerLauncher.UI\Assets\logo.ico" `
    --outputDir $releaseDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$fullPackagePath = Join-Path $releaseDir ("GameTrainerLauncher-" + $buildProps.Version + "-full.nupkg")
Assert-PackagedRuntimeConfig -packagePath $fullPackagePath

Write-Host "Done. Packages: $releaseDir" -ForegroundColor Green
