param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Tag = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

function Get-TagDescription {
    param(
        [string]$Tag
    )

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        return $null
    }

    $content = @(git for-each-ref ("refs/tags/" + $Tag) --format="%(contents)")
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $text = ($content -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text
}

function Get-ReleaseCommitLines {
    param(
        [string]$Version,
        [string]$Tag
    )

    $commitLines = @()

    if (-not [string]::IsNullOrWhiteSpace($Tag)) {
        $previousTag = git describe --tags --abbrev=0 "$Tag^" 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($previousTag)) {
            $commitLines = @(git log "$previousTag..$Tag" --pretty=format:"- %s" --no-merges)
        }
    }

    if ($commitLines.Count -eq 0) {
        $commitLines = @(git log -20 --pretty=format:"- %s" --no-merges)
    }

    $commitLines = @(
        $commitLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    if ($commitLines.Count -eq 0) {
        $commitLines = @("- Release $Version")
    }

    return $commitLines
}

$notes = Get-TagDescription -Tag $Tag
if ([string]::IsNullOrWhiteSpace($notes)) {
    $notesLines = @(
        "# Game Trainer Launcher v$Version",
        ""
    ) + (Get-ReleaseCommitLines -Version $Version -Tag $Tag)

    $notes = $notesLines -join [Environment]::NewLine
}

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $outputDir = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDir)) {
        New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    }

    Set-Content -Path $OutputPath -Value $notes -Encoding UTF8
}

$notes
