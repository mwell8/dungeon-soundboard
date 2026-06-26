param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\DungeonSoundboard.App\DungeonSoundboard.App.csproj"
$publishDir = Join-Path $root "publish\$Runtime"
$zipPath = Join-Path $root "publish\Dungeon-Soundboard-Windows-$Runtime.zip"

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet restore (Join-Path $root "DungeonSoundboard.Windows.sln")
dotnet test (Join-Path $root "DungeonSoundboard.Windows.sln") -c $Configuration
dotnet publish $project -c $Configuration -r $Runtime --self-contained $selfContained -o $publishDir

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Windows publish complete:"
Write-Host "  Folder: $publishDir"
Write-Host "  Zip:    $zipPath"
