# Apply ac_routes.txt to Other AC assets.
# Final-approach spacing is ALWAYS 5 NM between consecutive traffic (own between AC7 and AC8).
# Do not floor several leaders onto the same remaining.
# Unique first virtual points slide along the first-leg course only when the AC sits on that leg.
param([int]$OnlyLevel = 0)
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $Root "Assets\_Game\Data Files"
$RoutesFile = Join-Path $Root "Assets\_Game\Data Files\General\ac_routes.txt"
$Deltas = @(-35, -30, -25, -20, -15, -10, -5, 5, 10, 15)
$MinLiveRem = 1.0
$FtPerNm = 300.0
$OtherAcScript = "686a3e082b8fa1242ad16fff883efb61"

function New-ObjList { return ,(New-Object System.Collections.Generic.List[object]) }
function InvD([string]$s) {
  $n = 0.0
  [double]::TryParse($s, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$n) | Out-Null
  return $n
}
function Get-AllFiles([string]$dir) {
  $acc = New-Object System.Collections.Generic.List[string]
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
  $list = New-ObjList
  foreach ($m in [regex]::Matches($block, "guid:\s*([a-f0-9]{32})", "IgnoreCase")) {
    [void]$list.Add($m.Groups[1].Value.ToLower())
  }
  return ,$list
}
function Yaml-Field([string]$text, [string]$key) {
  $m = [regex]::Match($text, "${key}:[ \t]*([^\r\n]*)")
  if ($m.Success) { return $m.Groups[1].Value.Trim() }
  return ""
}
function Grab-Guid([string]$text, [string]$key) {
  $m = [regex]::Match($text, "${key}:\s*\{[^}]*guid:\s*([a-f0-9]{32})", "IgnoreCase")
  if ($m.Success) { return $m.Groups[1].Value.ToLower() }
  return $null
}
function Parse-LevelData([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  return @{
    filePath = $filePath
    mainRoute = Grab-Guid $text "MainRoute"
    aTCs = Grab-Guid $text "aTCs"
    levelInfo = Grab-Guid $text "levelInfo"
    virtualPoints = Grab-Guid $text "virtualPoints"
  }
}
function Parse-LevelInfo([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $n = 0
  [int]::TryParse((Yaml-Field $text "LevelNumber"), [ref]$n) | Out-Null
  return @{ LevelNumber = $n; Destination = Yaml-Field $text "Destination"; Runway = Yaml-Field $text "Runway" }
}
function Parse-Route([string]$filePath) {
  $points = New-ObjList
  $text = [IO.File]::ReadAllText($filePath)
  $m = [regex]::Match($text, "Points:\r?\n([\s\S]*?)(?:\r?\nm_|\r?\n---|$)")
  if (-not $m.Success) { return ,$points }
  $block = $m.Groups[1].Value
  if ($block -notmatch "^\r?\n") { $block = "`n" + $block }
  $items = $block -split "\r?\n\s*-\s*ID:"
  for ($i = 1; $i -lt $items.Count; $i++) {
    $item = $items[$i]
    $get = {
      param($k)
      $mm = [regex]::Match($item, "${k}:[ \t]*([^\r\n]*)")
      if ($mm.Success) { return $mm.Groups[1].Value.Trim() }
      return ""
    }
    $dist = InvD (& $get "Distance")
    $deg = InvD (& $get "RawDegrees")
    $spd = InvD (& $get "RawSpeed")
    [void]$points.Add(@{
      ID = ($i - 1); Name = & $get "Name"; Distance = $dist; RawDegrees = $deg
      RawSpeed = $spd; RawAltitude = & $get "RawAltitude"
    })
  }
  return ,$points
}
function Parse-Atc([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $chunks = $text -split "\r?\n\s*-\s*point:"
  $items = New-ObjList
  for ($i = 1; $i -lt $chunks.Count; $i++) {
    $chunk = $chunks[$i]
    $getN = {
      param($k)
      $mm = [regex]::Match($chunk, "${k}:[ \t]*([^\r\n]+)")
      if (-not $mm.Success) { return 0 }
      return (InvD $mm.Groups[1].Value.Trim())
    }
    $ptM = [regex]::Match($chunk, "^\s*(\d+)")
    [void]$items.Add(@{
      point = if ($ptM.Success) { [int]$ptM.Groups[1].Value } else { 0 }
      mode = [int](& $getN "mode")
      VS_nx = [int](& $getN "VS_nx")
    })
  }
  return ,$items
}
function Parse-VirtualPoints([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $items = New-ObjList
  foreach ($m in [regex]::Matches($text, "-\s*Number:\s*(\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)")) {
    [void]$items.Add(@{
      Number = [int]$m.Groups[1].Value
      x = InvD $m.Groups[2].Value
      y = InvD $m.Groups[3].Value
    })
  }
  return ,$items
}
function Next-Pos($start, [double]$distance, [double]$degrees) {
  $rad = $degrees * [Math]::PI / 180.0
  return @{ x = $start.x - $distance * [Math]::Sin($rad); y = $start.y + $distance * [Math]::Cos($rad) }
}
function Compute-Cartesian($points) {
  $cur = @{ x = 0.0; y = 0.0 }
  $out = New-ObjList
  $cum = 0.0
  for ($i = 0; $i -lt $points.Count; $i++) {
    $p = $points[$i]
    if ($i -gt 0) {
      $cur = Next-Pos $cur ([double]$p.Distance) (360.0 - [double]$p.RawDegrees)
      $cum += [double]$p.Distance
    }
    [void]$out.Add(@{
      ID = $i; Name = [string]$p.Name; Distance = [double]$p.Distance; RawDegrees = [double]$p.RawDegrees
      RawSpeed = $p.RawSpeed; RawAltitude = [string]$p.RawAltitude
      x = [double]$cur.x; y = [double]$cur.y; cumDist = $cum
    })
  }
  return ,$out
}
function Compute-VirtualWorld($routePts, $virtualItems) {
  $runway = @{ x = 0.0; y = 0.0 }
  if ($routePts.Count -gt 0) { $runway = @{ x = [double]$routePts[$routePts.Count - 1].x; y = [double]$routePts[$routePts.Count - 1].y } }
  $origin = @{ x = 0.0; y = 0.0 }
  if ($virtualItems.Count -gt 20) { $origin = @{ x = [double]$virtualItems[20].x; y = [double]$virtualItems[20].y } }
  $list = New-ObjList
  $n = $virtualItems.Count
  for ($j = 1; $j -le 20 -and ($j - 1) -lt $n; $j++) {
    $raw = $virtualItems[$j - 1]
    [void]$list.Add(@{
      Number = if ($raw.Number) { [int]$raw.Number } else { 50 + $j }
      x = $runway.x + [double]$raw.x - $origin.x
      y = $runway.y + [double]$raw.y - $origin.y
      rawX = [double]$raw.x
      rawY = [double]$raw.y
    })
  }
  return @{ runway = $runway; origin = $origin; list = $list }
}
function Get-Pos([int]$id, $routePts, $virtualPts) {
  if ($id -lt 50) {
    foreach ($p in $routePts) {
      if ([int]$p.ID -eq $id) { return @{ id = $id; x = [double]$p.x; y = [double]$p.y; kind = "star"; spd = [double]$p.RawSpeed } }
    }
    if ($id -ge 0 -and $id -lt $routePts.Count) {
      $p = $routePts[$id]
      return @{ id = $id; x = [double]$p.x; y = [double]$p.y; kind = "star"; spd = [double]$p.RawSpeed }
    }
    return $null
  }
  foreach ($v in $virtualPts) {
    if ([int]$v.Number -eq $id) { return @{ id = $id; x = [double]$v.x; y = [double]$v.y; kind = "virtual"; spd = 250.0 } }
  }
  return $null
}
function Dist($a, $b) {
  $dx = [double]$a.x - [double]$b.x
  $dy = [double]$a.y - [double]$b.y
  return [Math]::Sqrt($dx * $dx + $dy * $dy)
}
function Star-Index([int]$id, $routePts) {
  for ($i = 0; $i -lt $routePts.Count; $i++) { if ([int]$routePts[$i].ID -eq $id) { return $i } }
  return -1
}
function Push-Id($ids, [int]$id) {
  if ($id -eq 0) { return }
  if ($ids.Count -eq 0 -or [int]$ids[$ids.Count - 1] -ne $id) { [void]$ids.Add($id) }
}
function Ang-Abs([double]$a, [double]$b) {
  $d = [Math]::Abs($a - $b) % 360
  if ($d -gt 180) { $d = 360 - $d }
  return $d
}
function Bearing($a, $b) {
  $ang = [Math]::Atan2(([double]$b.x - [double]$a.x), ([double]$b.y - [double]$a.y)) * 180.0 / [Math]::PI
  if ($ang -lt 0) { $ang += 360 }
  return $ang
}
function Remaining-HasReversal([int]$fromIdx, $routePts) {
  $rw = $routePts[$routePts.Count - 1]
  for ($i = $fromIdx + 1; $i -lt $routePts.Count - 1; $i++) {
    $prev = $routePts[$i - 1]; $cur = $routePts[$i]
    if ((Ang-Abs (Bearing $prev $cur) (Bearing $cur $rw)) -gt 50) { return $true }
  }
  return $false
}
function Build-OwnIds($atc, $routePts, $virtualPts) {
  $ids = New-ObjList
  $rwId = [int]$routePts[$routePts.Count - 1].ID
  for ($ai = 0; $ai -lt $atc.Count; $ai++) {
    $a = $atc[$ai]
    $shortcut = ([int]$a.mode -eq 1 -or [int]$a.mode -eq 2)
    if (-not $shortcut -and $a.point -lt 50) {
      $cur = -1
      for ($k = $ids.Count - 1; $k -ge 0; $k--) { if ([int]$ids[$k] -lt 50) { $cur = [int]$ids[$k]; break } }
      $curIdx = Star-Index $cur $routePts
      $tgtIdx = Star-Index ([int]$a.point) $routePts
      if ($curIdx -ge 0 -and $tgtIdx -gt $curIdx) {
        for ($i = $curIdx + 1; $i -le $tgtIdx; $i++) { Push-Id $ids ([int]$routePts[$i].ID) }
      } else { Push-Id $ids ([int]$a.point) }
    } else {
      Push-Id $ids ([int]$a.point)
    }
    if ([int]$a.mode -eq 3 -or [int]$a.VS_nx -eq 3) {
      $last = if ($ids.Count) { [int]$ids[$ids.Count - 1] } else { -1 }
      if ($last -ge 0 -and $last -lt 50) {
        $from = Star-Index $last $routePts
        if ($from -ge 0 -and -not (Remaining-HasReversal $from $routePts)) {
          for ($i = $from + 1; $i -lt $routePts.Count; $i++) { Push-Id $ids ([int]$routePts[$i].ID) }
        } else {
          Push-Id $ids $rwId
        }
      } else {
        Push-Id $ids $rwId
      }
      break
    }
  }
  if ($ids.Count -eq 0 -or [int]$ids[$ids.Count - 1] -ne $rwId) { Push-Id $ids $rwId }
  return ,$ids
}
function Path-Len($pts) {
  $s = 0.0
  for ($i = 1; $i -lt $pts.Count; $i++) { $s += Dist $pts[$i - 1] $pts[$i] }
  return $s
}
function Alt-At([double]$rem, [double]$rwyAlt) {
  $raw = $rwyAlt + $rem * $FtPerNm
  if ($raw -lt $rwyAlt) { $raw = $rwyAlt }
  if ($raw -gt 30000) { $raw = 30000 }
  return [int][Math]::Round($raw)
}
function Parse-AcRoutes([string]$path) {
  $map = @{}
  $cur = 0
  foreach ($line in [IO.File]::ReadAllLines($path)) {
    $t = $line.Trim()
    if ($t -match '^LEVEL\s+(\d+)') { $cur = [int]$Matches[1]; if (-not $map.ContainsKey($cur)) { $map[$cur] = @{} }; continue }
    if ($t -match '^AC(\d+)\s*:\s*(.*)$' -and $cur -gt 0) {
      $ac = [int]$Matches[1]
      $rest = $Matches[2]
      $ids = New-ObjList
      foreach ($m in [regex]::Matches($rest, '\d+')) {
        $n = [int]$m.Value
        if ($ids.Count -eq 0 -or [int]$ids[$ids.Count - 1] -ne $n) { [void]$ids.Add($n) }
      }
      $map[$cur][$ac] = $ids
    }
  }
  return $map
}
function Build-Pts($ids, $routePts, $virtualPts) {
  $pts = New-ObjList
  foreach ($id in $ids) {
    $p = Get-Pos ([int]$id) $routePts $virtualPts
    if ($null -eq $p) { continue }
    if ($pts.Count -gt 0 -and (Dist $pts[$pts.Count - 1] $p) -lt 0.08) { continue }
    [void]$pts.Add($p)
  }
  return ,$pts
}
function Write-OtherAc([string]$filePath, [string]$name, $items, [double]$spawnRemaining) {
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.AppendLine("%YAML 1.1")
  [void]$sb.AppendLine("%TAG !u! tag:unity3d.com,2011:")
  [void]$sb.AppendLine("--- !u!114 &11400000")
  [void]$sb.AppendLine("MonoBehaviour:")
  [void]$sb.AppendLine("  m_ObjectHideFlags: 0")
  [void]$sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}")
  [void]$sb.AppendLine("  m_PrefabInstance: {fileID: 0}")
  [void]$sb.AppendLine("  m_PrefabAsset: {fileID: 0}")
  [void]$sb.AppendLine("  m_GameObject: {fileID: 0}")
  [void]$sb.AppendLine("  m_Enabled: 1")
  [void]$sb.AppendLine("  m_EditorHideFlags: 0")
  [void]$sb.AppendLine("  m_Script: {fileID: 11500000, guid: $OtherAcScript, type: 3}")
  [void]$sb.AppendLine("  m_Name: $name")
  [void]$sb.AppendLine("  m_EditorClassIdentifier: ")
  [void]$sb.AppendLine(("  SpawnRemaining: {0:0.0}" -f $spawnRemaining))
  [void]$sb.AppendLine("  ACItems:")
  foreach ($it in $items) {
    [void]$sb.AppendLine("  - Point: $($it.Point)")
    [void]$sb.AppendLine("    Altitude: $($it.Altitude)")
    [void]$sb.AppendLine("    Speed: $($it.Speed)")
  }
  $yaml = $sb.ToString().Replace("`r`n", "`n").Replace("`n", "`r`n")
  [IO.File]::WriteAllText($filePath, $yaml)
}

if (-not (Test-Path $RoutesFile)) { throw "Missing $RoutesFile" }
$routes = Parse-AcRoutes $RoutesFile
Write-Host "Indexing assets..."
$guidIndex = Build-GuidIndex
$configText = [IO.File]::ReadAllText((Join-Path $DataRoot "GameConfig.asset"))
$blockM = [regex]::Match($configText, "LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)")
$levelGuids = if ($blockM.Success) { Get-Guids $blockM.Groups[1].Value } else { New-ObjList }

$summary = New-ObjList
for ($li = 1; $li -lt $levelGuids.Count -and $li -le 39; $li++) {
  $dataPath = $guidIndex[$levelGuids[$li]]
  if (-not $dataPath) { Write-Host "SKIP index $li (no data)"; continue }
  $refs = Parse-LevelData $dataPath
  $info = Parse-LevelInfo $guidIndex[$refs.levelInfo]
  $n = if ($info.LevelNumber) { [int]$info.LevelNumber } else { $li }
  if ($OnlyLevel -gt 0 -and $n -ne $OnlyLevel) { continue }
  if (-not $routes.ContainsKey($n)) { Write-Host "SKIP Level $n (no ac_routes)"; continue }
  $routePts = Compute-Cartesian (Parse-Route $guidIndex[$refs.mainRoute])
  $vpRaw = Parse-VirtualPoints $guidIndex[$refs.virtualPoints]
  $world = Compute-VirtualWorld $routePts $vpRaw
  $runway = $world.runway; $origin = $world.origin; $virtualPts = $world.list
  $atc = Parse-Atc $guidIndex[$refs.aTCs]
  $ownIds = Build-OwnIds $atc $routePts $virtualPts
  $ownPts = Build-Pts $ownIds $routePts $virtualPts
  $ownLen = Path-Len $ownPts
  $rwyAlt = 0.0
  $rawRwy = [string]$routePts[$routePts.Count - 1].RawAltitude
  $rm = [regex]::Match($rawRwy, "\d+")
  if ($rm.Success) { $rwyAlt = InvD $rm.Value }
  $rwId = [int]$routePts[$routePts.Count - 1].ID
  $levelDir = Split-Path $dataPath
  $vpPath = $guidIndex[$refs.virtualPoints]

  $usage = @{}
  $firstUsage = @{}
  foreach ($ac in 1..10) {
    if (-not $routes[$n].ContainsKey($ac)) { continue }
    $acIds = $routes[$n][$ac]
    if ($acIds.Count -gt 0) {
      $fid = [int]$acIds[0]
      if ($firstUsage.ContainsKey($fid)) { $firstUsage[$fid]++ } else { $firstUsage[$fid] = 1 }
    }
    foreach ($id in $acIds) {
      $k = [int]$id
      if ($usage.ContainsKey($k)) { $usage[$k]++ } else { $usage[$k] = 1 }
    }
  }

  $vpMoves = @{}
  $levelLines = New-ObjList
  for ($ac = 1; $ac -le 10; $ac++) {
    if (-not $routes[$n].ContainsKey($ac)) { Write-Host "  L$n AC$ac missing"; continue }
    $ids = New-ObjList
    foreach ($id in $routes[$n][$ac]) { [void]$ids.Add([int]$id) }
    if ($ids.Count -eq 0 -or [int]$ids[$ids.Count - 1] -ne $rwId) { [void]$ids.Add($rwId) }
    $delta = $Deltas[$ac - 1]
    $spawnRem = $ownLen + $delta
    $pts = Build-Pts $ids $routePts $virtualPts
    if ($pts.Count -lt 2) { Write-Host "  L$n AC$ac too short"; continue }
    $pathLen0 = Path-Len $pts
    $firstId = [int]$pts[0].id
    $uniqueFirst = ($firstId -ge 51 -and $firstUsage.ContainsKey($firstId) -and [int]$firstUsage[$firstId] -eq 1)
    $firstVp = $null
    foreach ($v in $virtualPts) { if ([int]$v.Number -eq $firstId) { $firstVp = $v; break } }
    $stackedFirst = $false
    if ($firstVp) { $stackedFirst = [Math]::Abs([double]$firstVp.rawX) -lt 0.05 -and [Math]::Abs([double]$firstVp.rawY) -lt 0.05 }
    $p0 = $pts[0]; $p1 = $pts[1]
    $leg0 = Dist $p0 $p1
    $restAfterFirst = $pathLen0 - $leg0
    if ($restAfterFirst -lt 0) { $restAfterFirst = 0 }
    $onFirstLeg = ($spawnRem -gt ($restAfterFirst + 0.5))
    # Do not slide first VPs: clicked start points must stay put (traffic begins there).
    if ($false -and $uniqueFirst -and $onFirstLeg -and -not $vpMoves.ContainsKey($firstId)) {
      $dx = [double]$p0.x - [double]$p1.x
      $dy = [double]$p0.y - [double]$p1.y
      $leg = [Math]::Sqrt($dx * $dx + $dy * $dy)
      $ux = 0.0; $uy = 1.0
      if ($leg -ge 0.05 -and -not $stackedFirst) { $ux = $dx / $leg; $uy = $dy / $leg }
      elseif ($pts.Count -ge 3) {
        $p2 = $pts[2]
        $dx2 = [double]$p1.x - [double]$p2.x
        $dy2 = [double]$p1.y - [double]$p2.y
        $leg2 = [Math]::Sqrt($dx2 * $dx2 + $dy2 * $dy2)
        if ($leg2 -ge 0.05) { $ux = $dx2 / $leg2; $uy = $dy2 / $leg2 }
      } elseif ($leg -ge 0.05) { $ux = $dx / $leg; $uy = $dy / $leg }
      $needFirstLeg = $spawnRem - $restAfterFirst
      if ($needFirstLeg -lt 0.5) { $needFirstLeg = 0.5 }
      $nx = [double]$p1.x + $ux * $needFirstLeg
      $ny = [double]$p1.y + $uy * $needFirstLeg
      $rawX = $nx - [double]$runway.x + [double]$origin.x
      $rawY = $ny - [double]$runway.y + [double]$origin.y
      $vpMoves[$firstId] = @{ rawX = $rawX; rawY = $rawY; wx = $nx; wy = $ny }
      foreach ($v in $virtualPts) {
        if ([int]$v.Number -eq $firstId) { $v.x = $nx; $v.y = $ny; $v.rawX = $rawX; $v.rawY = $rawY }
      }
      $pts = Build-Pts $ids $routePts $virtualPts
    }
    $pathLen = Path-Len $pts
    $rems = New-Object 'double[]' $pts.Count
    if ($pts.Count -gt 0) { $rems[$pts.Count - 1] = 0 }
    for ($i = $pts.Count - 2; $i -ge 0; $i--) { $rems[$i] = $rems[$i + 1] + (Dist $pts[$i] $pts[$i + 1]) }
    $items = New-ObjList
    for ($i = 0; $i -lt $pts.Count; $i++) {
      $spd = 250
      if ($pts[$i].kind -eq "star" -and [double]$pts[$i].spd -gt 0) { $spd = [int]$pts[$i].spd }
      $altRem = $rems[$i]
      if ($i -eq 0) { $altRem = [Math]::Max($altRem, $spawnRem) }
      [void]$items.Add(@{ Point = [int]$pts[$i].id; Altitude = (Alt-At $altRem $rwyAlt); Speed = $spd })
    }
    $filePath = Join-Path $levelDir ("Other AC {0} Level {1}.asset" -f $ac, $n)
    if (-not (Test-Path $filePath)) {
      $hit = Get-ChildItem $levelDir -Filter ("Other AC {0} Level*.asset" -f $ac) | Select-Object -First 1
      if ($hit) { $filePath = $hit.FullName }
    }
    $name = "Other AC $ac Level $n"
    Write-OtherAc $filePath $name $items $spawnRem
    $vs = $spawnRem - $ownLen
    $live = if ($spawnRem -lt $MinLiveRem) { " SKIP" } else { "" }
    [void]$levelLines.Add(("  AC{0} pts {1} path {2:0.0} spawn {3:0.0} vsOwn {4:+0.0;-0.0} first {5}{6}" -f $ac, $pts.Count, $pathLen, $spawnRem, $vs, $firstId, $live))
  }

  if ($vpMoves.Count -gt 0 -and $vpPath) {
    $vpText = [IO.File]::ReadAllText($vpPath)
    foreach ($num in @($vpMoves.Keys)) {
      $mv = $vpMoves[$num]
      $rx = ("{0:0.######}" -f [double]$mv.rawX)
      $ry = ("{0:0.######}" -f [double]$mv.rawY)
      $pat = "(-\s*Number:\s*$num\s*\r?\n\s*x:\s*)[-+\d.]+(\s*\r?\n\s*y:\s*)[-+\d.]+"
      $m = [regex]::Match($vpText, $pat)
      if ($m.Success) {
        $vpText = $vpText.Substring(0, $m.Index) + $m.Groups[1].Value + $rx + $m.Groups[2].Value + $ry + $vpText.Substring($m.Index + $m.Length)
      } else {
        Write-Host ("  WARN no VP replace for {0}" -f $num)
      }
    }
    [IO.File]::WriteAllText($vpPath, $vpText)
  }

  Write-Host ("Level {0} {1} own {2:0.0} NM  vpMoves {3}" -f $n, $info.Destination, $ownLen, $vpMoves.Count)
  foreach ($line in $levelLines) { Write-Host $line }
  [void]$summary.Add($n)
}
Write-Host ("Updated {0} levels from {1}" -f $summary.Count, $RoutesFile)
