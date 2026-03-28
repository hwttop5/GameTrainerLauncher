param(
    [string]$XamlPath = "GameTrainerLauncher.UI\Views\UpdatePromptWindow.xaml"
)

$ErrorActionPreference = "Stop"

$fullPath = Resolve-Path $XamlPath
[xml]$doc = Get-Content $fullPath

$nsmgr = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
$nsmgr.AddNamespace("w", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
$nsmgr.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")

$legacyTextBox = $doc.SelectSingleNode("//w:TextBox[@x:Name='ReleaseNotesTextBox']", $nsmgr)
if ($null -ne $legacyTextBox) {
    throw "UI regression detected: Release notes panel must not use TextBox (ReleaseNotesTextBox)."
}

$releaseNotesText = $doc.SelectSingleNode("//w:TextBlock[@x:Name='ReleaseNotesTextBlock']", $nsmgr)
if ($null -eq $releaseNotesText) {
    throw "UI regression detected: ReleaseNotesTextBlock is missing."
}

$scrollViewer = $doc.SelectSingleNode("//w:ScrollViewer[w:TextBlock[@x:Name='ReleaseNotesTextBlock']]", $nsmgr)
if ($null -eq $scrollViewer) {
    throw "UI regression detected: ReleaseNotesTextBlock must be hosted inside ScrollViewer."
}

Write-Host "Update dialog UI regression check passed." -ForegroundColor Green
