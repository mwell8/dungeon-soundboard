param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [switch]$SkipRestore,
    [switch]$SkipTests,
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoRoot = Resolve-Path (Join-Path $root "..")
$project = Join-Path $root "src\DungeonSoundboard.App\DungeonSoundboard.App.csproj"
$publishRoot = Join-Path $root "publish"
$publishDir = Join-Path $publishRoot $Runtime
$zipName = "Dungeon-Soundboard-Windows-$Runtime.zip"
$normalizedVersion = ""

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $normalizedVersion = $Version.Trim()

    if ($normalizedVersion.StartsWith("windows-v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalizedVersion = $normalizedVersion.Substring("windows-v".Length)
    }
    elseif ($normalizedVersion.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalizedVersion = $normalizedVersion.Substring(1)
    }

    if (-not [string]::IsNullOrWhiteSpace($normalizedVersion)) {
        if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
            throw "Invalid release version: $Version"
        }

        $zipName = "Dungeon-Soundboard-$normalizedVersion-Windows-$Runtime.zip"
    }
}

$zipPath = Join-Path $publishRoot $zipName

$resolvedWindowsRoot = [System.IO.Path]::GetFullPath([string]$root)
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($publishRoot)
$windowsRootPrefix = $resolvedWindowsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedPublishRoot.StartsWith($windowsRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish path escaped the Windows workspace: $resolvedPublishRoot"
}

if ([System.IO.Directory]::Exists($resolvedPublishRoot)) {
    try {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force -ErrorAction Stop
    }
    catch [System.ComponentModel.Win32Exception] {
        # Extended paths let Windows remove legacy directories whose names end in a space.
        $extendedPublishRoot = "\\?\$resolvedPublishRoot"
        [System.IO.Directory]::Delete($extendedPublishRoot, $true)
    }
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

if (-not $SkipRestore) {
    dotnet restore (Join-Path $root "DungeonSoundboard.Windows.sln")
}

if (-not $SkipTests) {
    dotnet test (Join-Path $root "DungeonSoundboard.Windows.sln") -c $Configuration
}

$publishArgs = @(
    $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $selfContained,
    "-o", $publishDir,
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)

if ($SkipRestore) {
    $publishArgs += "--no-restore"
}

if (-not [string]::IsNullOrWhiteSpace($normalizedVersion)) {
    $publishArgs += "/p:Version=$normalizedVersion"
    $publishArgs += "/p:InformationalVersion=$normalizedVersion"
    $publishArgs += "/p:IncludeSourceRevisionInInformationalVersion=false"
}

dotnet publish @publishArgs

Get-ChildItem -LiteralPath $publishDir -Recurse -File -Filter "*.pdb" |
    Remove-Item -Force

$packageDocuments = @(
    "README-Windows.md",
    "README.md",
    "LICENSE"
)

foreach ($document in $packageDocuments) {
    $source = Join-Path $repoRoot $document
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $publishDir $document) -Force
    }
}

$requiredPackageFiles = @(
    "DungeonSoundboard.Windows.exe",
    "README-Windows.md",
    "README.md",
    "LICENSE"
)

foreach ($fileName in $requiredPackageFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $fileName))) {
        throw "Required package file is missing: $fileName"
    }
}

$forbiddenStateFiles = @(
    "playlists.json",
    "preferences.json",
    "hotkeys.json",
    "theme.json"
)

$publishedFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -File
foreach ($fileName in $forbiddenStateFiles) {
    if ($publishedFiles.Name -contains $fileName) {
        throw "User state must not be included in the release package: $fileName"
    }
}

if (Get-ChildItem -LiteralPath $publishDir -Recurse -Directory |
        Where-Object { $_.Name -eq "DemoAudio" } |
        Select-Object -First 1) {
    throw "Demo audio must not be included in the release package."
}

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Windows release zip was not created: $zipPath"
}

Write-Host "Windows publish complete:"
Write-Host "  Folder: $publishDir"
Write-Host "  Zip:    $zipPath"
