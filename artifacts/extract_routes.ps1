$ErrorActionPreference = "Stop"
$root = "C:\Users\User\NavigationShare\Assets\_Game\Data Files"
$outXlsx = "C:\Users\User\NavigationShare\artifacts\Level-Route-Chart-Restrictions.xlsx"
$outCsv = "C:\Users\User\NavigationShare\artifacts\Level-Route-Chart-Restrictions.csv"

function Get-YamlField {
    param([string]$Text, [string]$Key)
    $pattern = '(?m)^\s+' + [regex]::Escape($Key) + ':\s*(.*)\s*$'
    $m = [regex]::Match($Text, $pattern)
    if ($m.Success) { return $m.Groups[1].Value.Trim().Trim("'").Trim('"') }
    return ""
}

function Format-DbAlt([string]$raw) {
    if ([string]::IsNullOrWhiteSpace($raw)) { return "" }
    $v = $raw.Trim()
    if ($v -match '^(\d+)A(\d+)B$') { return "$($Matches[1])A / $($Matches[2])B (between)" }
    if ($v -match '^(\d+)A(\d+)$') { return "$($Matches[1])A / $($Matches[2]) (between, missing B)" }
    if ($v -match '^(\d+)\s+(\d+)$') { return "$($Matches[1]) / $($Matches[2]) (window, missing A/B)" }
    if ($v -match '^(\d+)C(\d+)$') { return "$($Matches[1]) / $($Matches[2]) (C used instead of A/B)" }
    if ($v -match '^(\d{5,})(\d{4,})$') { return "$v (concatenated, missing A/B)" }
    if ($v.EndsWith("A")) { return "$v (at or above)" }
    if ($v.EndsWith("B")) { return "$v (at or below)" }
    if ($v -match '^\d+$') { return "$v (at)" }
    return $v
}

function Format-DbSpd([string]$raw) {
    if ([string]::IsNullOrWhiteSpace($raw) -or $raw -eq "0") { return "" }
    return "$raw (MAX)"
}

# Level info
$infoByLevel = @{}
Get-ChildItem -Path $root -Recurse -Filter "Level Info*.asset" | Where-Object { $_.Extension -eq ".asset" } | ForEach-Object {
    $txt = Get-Content $_.FullName -Raw
    $n = Get-YamlField $txt "LevelNumber"
    if (-not $n) { return }
    $infoByLevel[$n] = [pscustomobject]@{
        Level = $n
        Destination = Get-YamlField $txt "Destination"
        Star = Get-YamlField $txt "Star"
        Transition = Get-YamlField $txt "Transition"
        Runway = Get-YamlField $txt "Runway"
        Freq = Get-YamlField $txt "Freq"
        Course = Get-YamlField $txt "Course"
    }
}

# Routes 1-39
$routes = @()
Get-ChildItem -Path (Join-Path $root "Level *") -Directory | ForEach-Object {
    $folder = $_.Name
    if ($folder -notmatch '^Level (\d+)$') { return }
    $lvl = [int]$Matches[1]
    if ($lvl -lt 1 -or $lvl -gt 39) { return }
    $routeFile = Get-ChildItem $_.FullName -Filter "*Route*.asset" | Where-Object { $_.Name -notlike "*original*" -and $_.Name -notlike "_ *" } | Select-Object -First 1
    if (-not $routeFile) {
        $routeFile = Get-ChildItem $_.FullName -Filter "*Route*.asset" | Select-Object -First 1
    }
    if (-not $routeFile) { return }
    $txt = Get-Content $routeFile.FullName -Raw
    $blocks = [regex]::Split($txt, "`r?`n  - ID:")
    $seq = 0
    for ($i = 1; $i -lt $blocks.Count; $i++) {
        $b = $blocks[$i]
        $name = Get-YamlField $b "Name"
        $spd = Get-YamlField $b "RawSpeed"
        $alt = Get-YamlField $b "RawAltitude"
        $details = Get-YamlField $b "Details"
        $dist = Get-YamlField $b "Distance"
        $hdg = Get-YamlField $b "RawDegrees"
        if ($name -match '^_START') { continue }
        $seq++
        $info = $infoByLevel["$lvl"]
        $routes += [pscustomobject]@{
            Level = $lvl
            File = $routeFile.Name
            ICAO = if ($info) { $info.Destination } else { "" }
            STAR = if ($info) { $info.Star } else { "" }
            Transition = if ($info) { $info.Transition } else { "" }
            Runway = if ($info) { $info.Runway } else { "" }
            Seq = $seq
            Waypoint = $name
            DbAltRaw = $alt
            DbSpdRaw = $spd
            DbAlt = Format-DbAlt $alt
            DbSpd = Format-DbSpd $spd
            Distance = $dist
            Heading = $hdg
            Details = $details
        }
    }
}

$routes | Sort-Object Level, Seq | ConvertTo-Csv -NoTypeInformation | Set-Content -Path (Join-Path $root "..\..\..\artifacts\_routes_raw.csv") -Encoding UTF8
# write next to artifacts
New-Item -ItemType Directory -Force -Path "C:\Users\User\NavigationShare\artifacts" | Out-Null
$routes | Sort-Object Level, Seq | ConvertTo-Csv -NoTypeInformation | Set-Content -Path "C:\Users\User\NavigationShare\artifacts\_routes_raw.csv" -Encoding UTF8
Write-Output ("ROWS=" + $routes.Count)
$routes | Sort-Object Level, Seq | ForEach-Object {
    "{0,2} {1,-5} {2,-8} {3,-12} alt=[{4}] spd=[{5}]" -f $_.Level, $_.ICAO, $_.Waypoint, $_.STAR, $_.DbAltRaw, $_.DbSpdRaw
}
