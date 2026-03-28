param(
    [string]$XamlPath = "GameTrainerLauncher.UI\Views\UpdatePromptWindow.xaml",
    [string]$AppStartupPath = "GameTrainerLauncher.UI\App.xaml.cs",
    [string]$ShortcutServicePath = "GameTrainerLauncher.UI\Services\ShortcutRepairService.cs"
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

$appStartupFullPath = Resolve-Path $AppStartupPath
$shortcutServiceFullPath = Resolve-Path $ShortcutServicePath

$appStartupContent = Get-Content $appStartupFullPath -Raw
if ($appStartupContent -notmatch "AddSingleton<IShortcutRepairService,\s*ShortcutRepairService>\(\)") {
    throw "Shortcut self-repair regression detected: service registration is missing in App.xaml.cs."
}
if ($appStartupContent -notmatch "shortcutRepairService\.RepairInstalledShortcuts\(\)") {
    throw "Shortcut self-repair regression detected: startup invocation is missing in App.xaml.cs."
}

$shortcutServiceContent = Get-Content $shortcutServiceFullPath -Raw
if ($shortcutServiceContent -notmatch "class\s+ShortcutRepairService") {
    throw "Shortcut self-repair regression detected: ShortcutRepairService implementation file is invalid."
}

Write-Host "Shortcut self-repair wiring check passed." -ForegroundColor Green
