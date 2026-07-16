param(
    [switch]$Force,
    [string]$DataDirectory = (Join-Path $env:APPDATA "DungeonSoundboard")
)

$ErrorActionPreference = "Stop"

function New-Id {
    return [guid]::NewGuid().ToString()
}

function New-UnicodeText {
    param(
        [int[]]$CodePoints
    )

    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Write-Ascii {
    param(
        [System.IO.BinaryWriter]$Writer,
        [string]$Value
    )

    $Writer.Write([System.Text.Encoding]::ASCII.GetBytes($Value))
}

function New-SilentWaveFile {
    param(
        [string]$Path,
        [double]$Seconds = 0.35
    )

    $sampleRate = 44100
    $channels = 1
    $bitsPerSample = 16
    $bytesPerSample = [int]($bitsPerSample / 8)
    $sampleCount = [int]($sampleRate * $Seconds)
    $dataSize = $sampleCount * $channels * $bytesPerSample
    $byteRate = $sampleRate * $channels * $bytesPerSample
    $blockAlign = $channels * $bytesPerSample

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        Write-Ascii $writer "RIFF"
        $writer.Write([int](36 + $dataSize))
        Write-Ascii $writer "WAVE"
        Write-Ascii $writer "fmt "
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]$channels)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]$byteRate)
        $writer.Write([int16]$blockAlign)
        $writer.Write([int16]$bitsPerSample)
        Write-Ascii $writer "data"
        $writer.Write([int]$dataSize)

        for ($i = 0; $i -lt $sampleCount; $i++) {
            $writer.Write([int16]0)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-Track {
    param(
        [string]$Title,
        [string]$Path,
        [string]$Role
    )

    return [ordered]@{
        id = New-Id
        title = $Title
        path = $Path
        role = $Role
        bookmarkData = $null
        volumeMultiplier = 1.0
    }
}

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    throw "DataDirectory is empty. Run this script on Windows with APPDATA set, or pass -DataDirectory."
}

$jsonFiles = @(
    "playlists.json",
    "preferences.json",
    "hotkeys.json",
    "theme.json"
)

$existingJsonFiles = @(
    foreach ($fileName in $jsonFiles) {
        $path = Join-Path $DataDirectory $fileName
        if (Test-Path -LiteralPath $path) {
            $path
        }
    }
)

$demoAudioDirectory = Join-Path $DataDirectory "DemoAudio"
if (($existingJsonFiles.Count -gt 0 -or (Test-Path -LiteralPath $demoAudioDirectory)) -and -not $Force) {
    throw "Demo state would overwrite existing DungeonSoundboard data. Re-run with -Force to create a backup and overwrite it."
}

New-Item -ItemType Directory -Force -Path $DataDirectory | Out-Null

if ($existingJsonFiles.Count -gt 0) {
    $backupDirectory = Join-Path $DataDirectory ("backup-demo-seed-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    foreach ($path in $existingJsonFiles) {
        Copy-Item -LiteralPath $path -Destination (Join-Path $backupDirectory (Split-Path -Leaf $path)) -Force
    }

    Write-Host "Backed up existing JSON to $backupDirectory"
}

if (Test-Path -LiteralPath $demoAudioDirectory) {
    Remove-Item -LiteralPath $demoAudioDirectory -Recurse -Force
}

$musicAudioDirectory = Join-Path $demoAudioDirectory "music"
$sfxAudioDirectory = Join-Path $demoAudioDirectory "sfx"

$musicTitles = @(
    "01 Instrument of Surrender",
    "16 Disco Elysium, pt 1",
    "17 Disco Elysium, pt 2"
)

$sfxTitles = @(
    "skill_1_intellect-made-with-Voicemod",
    "skill_2_psyche-made-with-Voicemod",
    "skill_3_physique-made-with-Voicemod",
    "skill_4_motorics-made-with-Voicemod",
    "skill_check-failure-made-with-Voicemod",
    "skill_check-success-made-with-Voicemod",
    "thought-complete-accept-01",
    "thought-gained-01"
)

$musicTracks = @(
    foreach ($title in $musicTitles) {
        $path = Join-Path $musicAudioDirectory ($title + ".wav")
        New-SilentWaveFile -Path $path
        New-Track -Title $title -Path $path -Role "music"
    }
)

$effectTracks = @(
    foreach ($title in $sfxTitles) {
        $path = Join-Path $sfxAudioDirectory ($title + ".wav")
        New-SilentWaveFile -Path $path
        New-Track -Title $title -Path $path -Role "effect"
    }
)

$mainPlaylistId = New-Id
$skillsPlaylistId = New-Id

$playlists = [ordered]@{
    musicPlaylists = @(
        [ordered]@{
            id = $mainPlaylistId
            name = (New-UnicodeText @(1054,1089,1085,1086,1074,1085,1086,1081,32,1087,1083,1077,1081,1083,1080,1089,1090))
            tracks = $musicTracks
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1044,1080,1089,1082,1086,32,1075,1088,1091,1089,1090,1100))
            tracks = @()
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1053,1072,1087,1088,1103,1078,1077,1085,1080,1077))
            tracks = @()
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1059,1078,1072,1089))
            tracks = @()
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1052,1080,1089,1090,1080,1082,1072))
            tracks = @()
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1063,1080,1083,1083))
            tracks = @()
        }
    )
    effectPlaylists = @(
        [ordered]@{
            id = $skillsPlaylistId
            name = (New-UnicodeText @(1053,1072,1074,1099,1082,1080))
            effects = $effectTracks
        },
        [ordered]@{
            id = New-Id
            name = (New-UnicodeText @(1057,1086,1073,1099,1090,1080,1103))
            effects = @()
        }
    )
}

$preferences = [ordered]@{
    volume = 0.8
    effectsVolume = 0.8
    repeatMode = "off"
    shuffleEnabled = $false
    musicFadeOutOnPauseEnabled = $false
    selectedMusicPlaylistId = $mainPlaylistId
    selectedEffectPlaylistId = $skillsPlaylistId
    duckingAmount = 0.55
    musicColumns = 3
    effectsColumns = 3
    language = "russian"
}

$jsonEncoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    (Join-Path $DataDirectory "playlists.json"),
    (($playlists | ConvertTo-Json -Depth 8) -replace "`r?`n", "`n"),
    $jsonEncoding)
[System.IO.File]::WriteAllText(
    (Join-Path $DataDirectory "preferences.json"),
    (($preferences | ConvertTo-Json -Depth 4) -replace "`r?`n", "`n"),
    $jsonEncoding)

Write-Host "Seeded Dungeon Soundboard demo state in $DataDirectory"
