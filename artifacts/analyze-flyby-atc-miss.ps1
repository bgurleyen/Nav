# Estimates LNAV fly-by closest approach to each fix vs ATC 1.3 NM capture.
# Model: lead = min(PilotSeekDistancePathFollow, inbound leg NM, outbound leg NM)
#        missNm = lead * tan(deflection/2)  (corner cut geometry)
$seek = 2.0   # _ SETTINGS _.asset PilotSeekDistancePathFollow
$atcCapture = 1.3

function Get-NextPos([double]$x, [double]$y, [double]$dist, [double]$rawDeg) {
    $deg = 360.0 - $rawDeg
    $rad = $deg * [Math]::PI / 180.0
    $nx = $x + $dist * [Math]::Sin($rad)
    $ny = $y + $dist * [Math]::Cos($rad)
    return @($nx, $ny)
}

function Get-LegHeading([double]$x1, [double]$y1, [double]$x2, [double]$y2) {
    $dx = $x2 - $x1
    $dy = $y2 - $y1
    if ([Math]::Abs($dx) -lt 1e-9 -and [Math]::Abs($dy) -lt 1e-9) { return $null }
    $h = [Math]::Atan2($dx, $dy) * 180.0 / [Math]::PI
    if ($h -lt 0) { $h += 360.0 }
    return $h
}

function Get-Deflection([double]$h1, [double]$h2) {
    $d = [Math]::Abs($h1 - $h2)
    if ($d -gt 180) { $d = 360 - $d }
    return $d
}

function Parse-RoutePoints([string]$content) {
    $points = @()
    $blocks = [regex]::Split($content, '(?=  - ID: )')
    foreach ($b in $blocks) {
        if ($b -notmatch 'Name:\s*(\S+)') { continue }
        $name = $Matches[1]
        $raw = 0.0; $dist = 0.0; $id = -1
        if ($b -match 'ID:\s*(\d+)') { $id = [int]$Matches[1] }
        if ($b -match 'RawDegrees:\s*([\d\.]+)') { $raw = [double]$Matches[1] }
        if ($b -match 'Distance:\s*([\d\.]+)') { $dist = [double]$Matches[1] }
        $points += [pscustomobject]@{ Id = $id; Name = $name; Raw = $raw; Dist = $dist }
    }
    return $points
}

function Parse-AtcPoints([string]$content) {
    $pts = @()
    foreach ($m in [regex]::Matches($content, 'point:\s*(\d+)')) {
        $pts += [int]$m.Groups[1].Value
    }
    return $pts
}

$root = Join-Path $PSScriptRoot '..'
$data = Join-Path $root 'Assets\_Game\Data Files'
$rows = @()

Get-ChildItem -Path $data -Filter 'Route *.asset' -Recurse | ForEach-Object {
    $levelDir = $_.Directory.Name
    if ($levelDir -notmatch 'Level\s+(\d+)') { return }
    $level = [int]$Matches[1]
    $routeText = Get-Content -Raw $_.FullName
    $points = Parse-RoutePoints $routeText
    if ($points.Count -lt 3) { return }

    $atcFile = Join-Path $_.Directory.FullName ("ATC Instructions Level {0}.asset" -f $level)
    $atcTargets = @()
    if (Test-Path $atcFile) {
        $atcTargets = Parse-AtcPoints (Get-Content -Raw $atcFile)
    }

    $x = 0.0; $y = 0.0
    $pos = @(@($x, $y))
    for ($i = 1; $i -lt $points.Count; $i++) {
        $p = $points[$i]
        $np = Get-NextPos $x $y $p.Dist $p.Raw
        $x = $np[0]; $y = $np[1]
        $pos += ,@($x, $y)
    }

    for ($i = 1; $i -lt ($points.Count - 1); $i++) {
        $wp = $points[$i]
        if ($wp.Name -match '^_(START|END)_|^RW|^CI|^FI|^DISC$') { continue }

        $inLeg = $points[$i].Dist
        $outLeg = $points[$i + 1].Dist
        $hin = Get-LegHeading $pos[$i-1][0] $pos[$i-1][1] $pos[$i][0] $pos[$i][1]
        $hout = Get-LegHeading $pos[$i][0] $pos[$i][1] $pos[$i+1][0] $pos[$i+1][1]
        if ($null -eq $hin -or $null -eq $hout) { continue }

        $defl = Get-Deflection $hin $hout
        $lead = [Math]::Min($seek, [Math]::Min($inLeg, $outLeg))
        $halfRad = ($defl / 2.0) * [Math]::PI / 180.0
        $miss = $lead * [Math]::Tan($halfRad)

        if ($miss -le $atcCapture + 0.05) { continue }

        $isAtc = $atcTargets -contains $wp.Id
        $rows += [pscustomobject]@{
            Level = $level
            RouteIndex = $wp.Id
            Name = $wp.Name
            DeflectionDeg = [Math]::Round($defl, 1)
            InLegNm = [Math]::Round($inLeg, 1)
            OutLegNm = [Math]::Round($outLeg, 1)
            EstMissNm = [Math]::Round($miss, 2)
            AtcTarget = $isAtc
        }
    }
}

$outCsv = Join-Path $PSScriptRoot 'flyby-atc-miss-estimate.csv'
$rows | Sort-Object Level, RouteIndex | Export-Csv -NoTypeInformation -Path $outCsv

Write-Host "Seek=$seek NM, ATC capture=$atcCapture NM, corners with est miss > $($atcCapture+0.05): $($rows.Count)"
Write-Host "ATC targets among them: $(($rows | Where-Object AtcTarget).Count)"
Write-Host "CSV: $outCsv"
Write-Host ""
Write-Host "=== ATC instruction targets only ==="
$rows | Where-Object AtcTarget | Sort-Object Level, RouteIndex | Format-Table -AutoSize
