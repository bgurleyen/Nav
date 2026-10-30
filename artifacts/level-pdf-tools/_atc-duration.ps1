$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $Root "Assets\_Game\Data Files"

function Get-AllFiles([string]$dir) {
  $acc = New-Object System.Collections.Generic.List[string]
  if (-not (Test-Path $dir)) { return $acc }
  $stack = New-Object System.Collections.Stack
  $stack.Push($dir)
  while ($stack.Count -gt 0) {
    $cur = $stack.Pop()
    foreach ($d in [IO.Directory]::GetDirectories($cur)) { $stack.Push($d) }
    foreach ($f in [IO.Directory]::GetFiles($cur)) { $acc.Add($f) }
  }
  return $acc
}
function Build-GuidIndex {
  $index = @{}
  foreach ($metaPath in (Get-AllFiles $DataRoot | Where-Object { $_ -like "*.meta" })) {
    $text = [IO.File]::ReadAllText($metaPath)
    $m = [regex]::Match($text, "guid:\s*([a-f0-9]{32})", "IgnoreCase")
    if (-not $m.Success) { continue }
    $assetPath = $metaPath -replace "\.meta$", ""
    if (Test-Path $assetPath) { $index[$m.Groups[1].Value.ToLower()] = $assetPath }
  }
  return $index
}
function Get-Guids([string]$block) {
  $list = New-Object System.Collections.Generic.List[string]
  foreach ($m in [regex]::Matches($block, "guid:\s*([a-f0-9]{32})", "IgnoreCase")) {
    [void]$list.Add($m.Groups[1].Value.ToLower())
  }
  return $list
}
function Parse-LevelData([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $grab = {
    param($k)
    $m = [regex]::Match($text, "${k}:\s*\{[^}]*guid:\s*([a-f0-9]{32})", "IgnoreCase")
    if ($m.Success) { return $m.Groups[1].Value.ToLower() }
    return $null
  }
  return @{
    mainRoute     = & $grab "MainRoute"
    aTCs          = & $grab "aTCs"
    levelInfo     = & $grab "levelInfo"
    virtualPoints = & $grab "virtualPoints"
  }
}
function Read-YamlField([string]$text, [string]$key) {
  $m = [regex]::Match($text, "${key}:[ \t]*([^\r\n]*)")
  if ($m.Success) { return $m.Groups[1].Value.Trim() }
  return ""
}
function Parse-LevelInfo([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $n = 0.0
  $lv = Read-YamlField $text "LevelNumber"
  if (-not [double]::TryParse($lv, [ref]$n)) { $n = 0 }
  return @{ LevelNumber = [int]$n; Destination = Read-YamlField $text "Destination" }
}
function Parse-Route([string]$filePath) {
  $points = New-Object System.Collections.Generic.List[object]
  $text = [IO.File]::ReadAllText($filePath)
  $m = [regex]::Match($text, "Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)")
  if (-not $m.Success) { return $points }
  $items = $m.Groups[1].Value -split "\r?\n\s*-\s*ID:"
  for ($i = 1; $i -lt $items.Count; $i++) {
    $item = $items[$i]
    $get = {
      param($k)
      $mm = [regex]::Match($item, "${k}:[ \t]*([^\r\n]*)")
      if ($mm.Success) { return $mm.Groups[1].Value.Trim() }
      return ""
    }
    $idM = [regex]::Match($item, "^\s*(\d+)")
    $id = if ($idM.Success) { [int]$idM.Groups[1].Value } else { 0 }
    $dist = 0.0; [double]::TryParse((& $get "Distance"), [ref]$dist) | Out-Null
    $deg = 0.0; [double]::TryParse((& $get "RawDegrees"), [ref]$deg) | Out-Null
    [void]$points.Add(@{ ID = $id; Name = & $get "Name"; RawDegrees = $deg; Distance = $dist })
  }
  return $points
}
function Parse-Atc([string]$filePath) {
  $items = New-Object System.Collections.Generic.List[object]
  $text = [IO.File]::ReadAllText($filePath)
  $chunks = $text -split "\r?\n\s*-\s*point:"
  for ($i = 1; $i -lt $chunks.Count; $i++) {
    $chunk = $chunks[$i]
    $ptM = [regex]::Match($chunk, "^\s*(\d+)")
    $modeM = [regex]::Match($chunk, "mode:[ \t]*([^\r\n]+)")
    $vsM = [regex]::Match($chunk, "VS_nx:[ \t]*([^\r\n]+)")
    $pt = if ($ptM.Success) { [int]$ptM.Groups[1].Value } else { 0 }
    $mode = 0; if ($modeM.Success) { [int]::TryParse($modeM.Groups[1].Value.Trim(), [ref]$mode) | Out-Null }
    $vs = 0; if ($vsM.Success) { [int]::TryParse($vsM.Groups[1].Value.Trim(), [ref]$vs) | Out-Null }
    [void]$items.Add(@{ point = $pt; mode = $mode; VS_nx = $vs })
  }
  return $items
}
function Parse-VirtualPoints([string]$filePath) {
  $items = New-Object System.Collections.Generic.List[object]
  $text = [IO.File]::ReadAllText($filePath)
  foreach ($m in [regex]::Matches($text, "-\s*Number:\s*(\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)")) {
    [void]$items.Add(@{ Number = [int]$m.Groups[1].Value; x = [double]$m.Groups[2].Value; y = [double]$m.Groups[3].Value })
  }
  return $items
}
function DegreesOf([double]$raw) { return 360.0 - $raw }
function Next-Pos($start, [double]$distance, [double]$degrees) {
  $rad = $degrees * [Math]::PI / 180.0
  return @{ x = $start.x - $distance * [Math]::Sin($rad); y = $start.y + $distance * [Math]::Cos($rad) }
}
function Compute-Cartesian($points) {
  $cur = @{ x = 0.0; y = 0.0 }
  $out = New-Object System.Collections.Generic.List[object]
  $cum = 0.0
  for ($i = 0; $i -lt $points.Count; $i++) {
    $p = $points[$i]
    if ($i -gt 0) {
      $cur = Next-Pos $cur ([double]$p.Distance) (DegreesOf ([double]$p.RawDegrees))
      $cum += [double]$p.Distance
    }
    [void]$out.Add(@{
      ID = [int]$p.ID; Name = [string]$p.Name; RawDegrees = [double]$p.RawDegrees; Distance = [double]$p.Distance
      x = [double]$cur.x; y = [double]$cur.y; cum = $cum
    })
  }
  return $out
}
function Compute-VirtualWorld($routePts, $virtualItems) {
  $runway = @{ x = 0.0; y = 0.0 }
  if ($routePts.Count -gt 0) { $runway = $routePts[$routePts.Count - 1] }
  $origin = @{ x = 0.0; y = 0.0 }
  if ($virtualItems.Count -gt 20) { $origin = $virtualItems[20] }
  $list = New-Object System.Collections.Generic.List[object]
  for ($j = 1; $j -le 20 -and ($j - 1) -lt $virtualItems.Count; $j++) {
    $raw = $virtualItems[$j - 1]
    $num = if ($raw.Number) { [int]$raw.Number } else { 50 + $j }
    [void]$list.Add(@{
      Number = $num
      x = [double]$runway.x + [double]$raw.x - [double]$origin.x
      y = [double]$runway.y + [double]$raw.y - [double]$origin.y
    })
  }
  return $list
}
function Resolve-Point([int]$pointId, $routePts, $virtualPts) {
  if ($pointId -lt 50) {
    $p = $null
    foreach ($q in $routePts) { if ([int]$q.ID -eq $pointId) { $p = $q; break } }
    if ($null -eq $p -and $pointId -ge 0 -and $pointId -lt $routePts.Count) { $p = $routePts[$pointId] }
    if ($null -eq $p) { return @{ id = $pointId; x = 0.0; y = 0.0 } }
    return @{ id = [int]$p.ID; x = [double]$p.x; y = [double]$p.y; cum = [double]$p.cum }
  }
  $v = $null
  foreach ($q in $virtualPts) { if ([int]$q.Number -eq $pointId) { $v = $q; break } }
  if ($null -eq $v) { return @{ id = $pointId; x = 0.0; y = 0.0 } }
  return @{ id = [int]$v.Number; x = [double]$v.x; y = [double]$v.y }
}
function Hypot([double]$dx, [double]$dy) { return [Math]::Sqrt($dx * $dx + $dy * $dy) }
function Polyline-Len($pts) {
  $s = 0.0
  for ($i = 1; $i -lt $pts.Count; $i++) {
    $s += Hypot ([double]$pts[$i].x - [double]$pts[$i - 1].x) ([double]$pts[$i].y - [double]$pts[$i - 1].y)
  }
  return $s
}
function Star-IndexOf($routePts, [int]$pointId) {
  if ($pointId -ge 50) { return -1 }
  for ($i = 0; $i -lt $routePts.Count; $i++) {
    if ([int]$routePts[$i].ID -eq $pointId) { return $i }
  }
  return $pointId
}
function Atc-FlownPath($atc, $routePts, $virtualPts) {
  $path = New-Object System.Collections.Generic.List[object]
  $push = {
    param($p, $point, $mode)
    if ($null -eq $p) { return }
    if ($path.Count -eq 0) {
      [void]$path.Add(@{ x = [double]$p.x; y = [double]$p.y; point = $point; mode = $mode })
      return
    }
    $last = $path[$path.Count - 1]
    if (Hypot ([double]$last.x - [double]$p.x) ([double]$last.y - [double]$p.y) -gt 0.05) {
      [void]$path.Add(@{ x = [double]$p.x; y = [double]$p.y; point = $point; mode = $mode })
    }
  }
  if ($routePts.Count -gt 0) {
    & $push $routePts[0] $routePts[0].ID 0
  }
  $ils = $false
  foreach ($a in $atc) {
    $r = Resolve-Point ([int]$a.point) $routePts $virtualPts
    $shortcut = ([int]$a.mode -eq 1 -or [int]$a.mode -eq 2)
    if (-not $shortcut -and [int]$a.point -lt 50 -and $routePts.Count -gt 0) {
      $curIdx = if ($path.Count -gt 0) { Star-IndexOf $routePts ([int]$path[$path.Count - 1].point) } else { Star-IndexOf $routePts ([int]$routePts[0].ID) }
      $tgtIdx = Star-IndexOf $routePts ([int]$a.point)
      if ($curIdx -ge 0 -and $tgtIdx -gt $curIdx) {
        for ($i = $curIdx + 1; $i -le $tgtIdx; $i++) { & $push $routePts[$i] $routePts[$i].ID 0 }
      } else {
        & $push $r ([int]$a.point) ([int]$a.mode)
      }
    } else {
      & $push $r ([int]$a.point) ([int]$a.mode)
    }
    if ([int]$a.mode -eq 3 -or [int]$a.VS_nx -eq 3) { $ils = $true }
  }
  if ($routePts.Count -gt 0) {
    $lastStar = $null
    for ($i = $path.Count - 1; $i -ge 0; $i--) {
      if ([int]$path[$i].point -lt 50) { $lastStar = $path[$i]; break }
    }
    $from = if ($lastStar) { Star-IndexOf $routePts ([int]$lastStar.point) } else { 0 }
    $startRemain = if ($ils -or $from -ge 0) { [Math]::Max($from, $routePts.Count - 3) } else { $routePts.Count - 3 }
    for ($i = $startRemain; $i -lt $routePts.Count; $i++) { & $push $routePts[$i] $routePts[$i].ID 9 }
  }
  return $path
}
function Format-Duration([double]$nm, [double]$gsKts) {
  $sec = [int][Math]::Round(($nm / $gsKts) * 3600.0)
  $m = [Math]::Floor($sec / 60)
  $s = $sec % 60
  return "{0}:{1:D2}" -f $m, $s
}

$guidIndex = Build-GuidIndex
$configText = [IO.File]::ReadAllText((Join-Path $DataRoot "GameConfig.asset"))
$blockM = [regex]::Match($configText, "LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)")
$levelGuids = if ($blockM.Success) { Get-Guids $blockM.Groups[1].Value } else { @() }

$rows = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $levelGuids.Count; $i++) {
  if ($i -lt 1 -or $i -gt 39) { continue }
  $dataPath = $guidIndex[$levelGuids[$i]]
  if (-not $dataPath) { continue }
  $refs = Parse-LevelData $dataPath
  $info = if ($refs.levelInfo -and $guidIndex.ContainsKey($refs.levelInfo)) {
    Parse-LevelInfo $guidIndex[$refs.levelInfo]
  } else {
    @{ LevelNumber = $i; Destination = "?" }
  }
  $routePts = if ($refs.mainRoute -and $guidIndex.ContainsKey($refs.mainRoute)) {
    Compute-Cartesian (Parse-Route $guidIndex[$refs.mainRoute])
  } else {
    @()
  }
  $vpRaw = if ($refs.virtualPoints -and $guidIndex.ContainsKey($refs.virtualPoints)) {
    Parse-VirtualPoints $guidIndex[$refs.virtualPoints]
  } else {
    @()
  }
  $virtualPts = Compute-VirtualWorld $routePts $vpRaw
  $atc = if ($refs.aTCs -and $guidIndex.ContainsKey($refs.aTCs)) { Parse-Atc $guidIndex[$refs.aTCs] } else { @() }
  $flown = Atc-FlownPath $atc $routePts $virtualPts
  $atcLen = Polyline-Len $flown
  [void]$rows.Add([pscustomobject]@{
    Level = if ($info.LevelNumber) { $info.LevelNumber } else { $i }
    Dest  = $info.Destination
    AtcNm = [Math]::Round($atcLen, 2)
    Dur240 = Format-Duration $atcLen 240.0
  })
}

$max = $rows | Sort-Object AtcNm -Descending | Select-Object -First 1
$min = $rows | Sort-Object AtcNm | Select-Object -First 1
Write-Output "GS=240kt constant (time = NM / GS)"
Write-Output ("Longest: Level {0} ({1}) = {2} NM => {3}" -f $max.Level, $max.Dest, $max.AtcNm, $max.Dur240)
Write-Output ("Shortest: Level {0} ({1}) = {2} NM => {3}" -f $min.Level, $min.Dest, $min.AtcNm, $min.Dur240)
Write-Output ""
$rows | Sort-Object Level | ForEach-Object {
  "{0,2} {1,-6} {2,7} NM  {3}" -f $_.Level, $_.Dest, $_.AtcNm, $_.Dur240
}
