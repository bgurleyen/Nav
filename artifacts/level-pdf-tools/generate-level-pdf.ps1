# One page per level: STAR fills the page; off-map virtual points sit on the edge.
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $Root "Assets\_Game\Data Files"
$OutPdf = Join-Path $Root "artifacts\Level-Routes-ATC.pdf"

$PageW = 1191.0
$PageH = 842.0
$ModeNames = @{ 0 = "NO CHANGE"; 1 = "DCT"; 2 = "HDG"; 3 = "CLR ILS" }

$HelvW = @{
  32=278;33=278;34=355;35=556;36=556;37=889;38=667;39=191;40=333;41=333;42=389;43=584;44=278;45=333;46=278;47=278
  48=556;49=556;50=556;51=556;52=556;53=556;54=556;55=556;56=556;57=556;58=278;59=278;60=584;61=584;62=584;63=556
  64=1015;65=667;66=667;67=722;68=722;69=667;70=611;71=778;72=722;73=278;74=500;75=667;76=556;77=833;78=722;79=778
  80=667;81=778;82=722;83=667;84=611;85=722;86=667;87=944;88=667;89=667;90=611;91=278;92=278;93=278;94=469;95=556
  96=333;97=556;98=556;99=500;100=556;101=556;102=278;103=556;104=556;105=222;106=222;107=500;108=222;109=833;110=556;111=556
  112=556;113=556;114=333;115=500;116=278;117=556;118=500;119=722;120=500;121=500;122=500;123=334;124=260;125=334;126=584
}
$HelvBoldW = @{
  32=278;33=333;34=474;35=556;36=556;37=889;38=722;39=238;40=333;41=333;42=389;43=584;44=278;45=333;46=278;47=278
  48=556;49=556;50=556;51=556;52=556;53=556;54=556;55=556;56=556;57=556;58=333;59=333;60=584;61=584;62=584;63=611
  64=975;65=722;66=722;67=722;68=722;69=667;70=611;71=778;72=722;73=278;74=556;75=722;76=611;77=833;78=722;79=778
  80=667;81=778;82=722;83=667;84=611;85=722;86=667;87=944;88=667;89=667;90=611;91=333;92=278;93=333;94=584;95=556
  96=333;97=556;98=611;99=556;100=611;101=556;102=333;103=611;104=611;105=278;106=278;107=556;108=278;109=889;110=611;111=611
  112=611;113=611;114=389;115=556;116=333;117=611;118=556;119=778;120=556;121=556;122=500;123=389;124=280;125=389;126=584
}

function New-ObjList { return ,(New-Object System.Collections.Generic.List[object]) }

function Get-TextWidth([string]$text, [double]$size, [bool]$bold) {
  $map = if ($bold) { $HelvBoldW } else { $HelvW }
  $w = 0.0
  foreach ($ch in $text.ToCharArray()) {
    $c = [int]$ch
    if ($map.ContainsKey($c)) { $w += $map[$c] } else { $w += 556 }
  }
  return $w * $size / 1000.0
}
function ConvertTo-Ascii([string]$text) {
  if ($null -eq $text) { return "" }
  return -join ($text.ToCharArray() | ForEach-Object { if ([int]$_ -lt 128) { $_ } else { "?" } })
}
function Fit-Text([string]$text, [double]$size, [double]$maxWidth, [bool]$bold) {
  $t = ConvertTo-Ascii $text
  while ($t.Length -gt 1 -and (Get-TextWidth $t $size $bold) -gt $maxWidth) { $t = $t.Substring(0, $t.Length - 1) }
  if ($t -ne (ConvertTo-Ascii $text) -and $t.Length -gt 1) { $t = $t.Substring(0, $t.Length - 1) + "..." }
  return $t
}
function Format-Alt($raw) {
  if ($null -eq $raw -or $raw -eq "" -or $raw -eq 0 -or $raw -eq "0") { return "" }
  $n = 0.0
  if ([double]::TryParse([string]$raw, [ref]$n) -and $n -ge 10000 -and ($n % 100 -eq 0)) {
    return ("FL{0:000}" -f [int]($n / 100))
  }
  return [string]$raw
}
function Format-StarAlt($raw) {
  $s = [string]$raw
  if ([string]::IsNullOrWhiteSpace($s) -or $s -eq "0") { return "" }
  if ($s -match '[ABab]') {
    $s = $s.ToUpper()
    $s = [regex]::Replace($s, '([AB])(\d)', '$1/$2')
    return $s
  }
  return Format-Alt $s
}
function Format-StarSpd($rawSpeed) {
  $n = 0
  [int]::TryParse([string]$rawSpeed, [ref]$n) | Out-Null
  if ($n -le 0) { return "" }
  return "${n}kt"
}
function Format-Nx([int]$nx, [string]$value) {
  if ([string]::IsNullOrWhiteSpace($value)) { return "" }
  switch ($nx) {
    1 { return "$value A" }
    2 { return "$value B" }
    default { return $value }
  }
}
function Format-AtcAlt($a) {
  if (-not $a -or -not $a.Altitude -or $a.Altitude -eq 0) { return "" }
  return Format-Nx ([int]$a.VS_nx) (Format-Alt $a.Altitude)
}
function Format-AtcSpd($a) {
  if (-not $a -or -not $a.Speed -or $a.Speed -eq 0) { return "" }
  $v = "$($a.Speed)kt"
  switch ([int]$a.Speed_nx) {
    1 { return "$v MIN" }
    2 { return "$v MAX" }
    default { return $v }
  }
}
function Test-IsIlsClr($a) {
  if (-not $a) { return $false }
  return ([int]$a.mode -eq 3 -or [int]$a.VS_nx -eq 3)
}
function Get-ModeLabel($a) {
  if ($a.VS_nx -eq 3 -or $a.mode -eq 3) { return "CLR ILS" }
  if ($ModeNames.ContainsKey($a.mode)) { return $ModeNames[$a.mode] }
  return "M$($a.mode)"
}
function Escape-Pdf([string]$t) {
  return ((ConvertTo-Ascii $t) -replace '\\', '\\' -replace '\(', '\(' -replace '\)', '\)')
}

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
  $list = New-ObjList
  foreach ($m in [regex]::Matches($block, "guid:\s*([a-f0-9]{32})", "IgnoreCase")) {
    [void]$list.Add($m.Groups[1].Value.ToLower())
  }
  return ,$list
}
function Read-YamlField([string]$text, [string]$key) {
  $m = [regex]::Match($text, "${key}:[ \t]*([^\r\n]*)")
  if ($m.Success) { return $m.Groups[1].Value.Trim() }
  return ""
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
function Parse-LevelInfo([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $num = {
    param($k)
    $v = Read-YamlField $text $k
    $n = 0.0
    if ([double]::TryParse($v, [ref]$n)) { return $n }
    return 0
  }
  return @{
    LevelNumber = [int](& $num "LevelNumber")
    Destination = Read-YamlField $text "Destination"
    Star        = Read-YamlField $text "Star"
    Transition  = Read-YamlField $text "Transition"
    Runway      = Read-YamlField $text "Runway"
    Freq        = Read-YamlField $text "Freq"
    Course      = & $num "Course"
    CrzAltitude = & $num "CrzAltitude"
    GlideSlope  = & $num "GlideSlope"
  }
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
    $idM = [regex]::Match($item, "^\s*(\d+)")
    $id = if ($idM.Success) { [int]$idM.Groups[1].Value } else { 0 }
    $dist = 0.0; [double]::TryParse((& $get "Distance"), [ref]$dist) | Out-Null
    $deg = 0.0; [double]::TryParse((& $get "RawDegrees"), [ref]$deg) | Out-Null
    $spd = 0.0; [double]::TryParse((& $get "RawSpeed"), [ref]$spd) | Out-Null
    [void]$points.Add(@{
      ID = $id; Name = & $get "Name"; RawDegrees = $deg; Distance = $dist
      RawAltitude = & $get "RawAltitude"; RawSpeed = $spd
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
      if ($mm.Success) {
        $n = 0.0
        [double]::TryParse($mm.Groups[1].Value.Trim(), [ref]$n) | Out-Null
        return $n
      }
      return 0
    }
    $ptM = [regex]::Match($chunk, "^\s*(\d+)")
    [void]$items.Add(@{
      point = if ($ptM.Success) { [int]$ptM.Groups[1].Value } else { 0 }
      mode = [int](& $getN "mode")
      Altitude = [long](& $getN "Altitude")
      VS_nx = [int](& $getN "VS_nx")
      Speed = [int](& $getN "Speed")
      Speed_nx = [int](& $getN "Speed_nx")
    })
  }
  return ,$items
}
function Parse-VirtualPoints([string]$filePath) {
  $text = [IO.File]::ReadAllText($filePath)
  $items = New-ObjList
  foreach ($m in [regex]::Matches($text, "-\s*Number:\s*(\d+)\s*\r?\n\s*x:\s*([-\d.]+)\s*\r?\n\s*y:\s*([-\d.]+)")) {
    [void]$items.Add(@{ Number = [int]$m.Groups[1].Value; x = [double]$m.Groups[2].Value; y = [double]$m.Groups[3].Value })
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
      ID = $i; Name = [string]$p.Name; RawDegrees = [double]$p.RawDegrees; Distance = [double]$p.Distance
      RawAltitude = [string]$p.RawAltitude; RawSpeed = $p.RawSpeed
      x = [double]$cur.x; y = [double]$cur.y; cumDist = $cum
    })
  }
  return ,$out
}
function Compute-VirtualWorld($routePts, $virtualItems) {
  $runway = @{ x = 0.0; y = 0.0 }
  if ($routePts -and $routePts.Count -gt 0) { $runway = $routePts[$routePts.Count - 1] }
  $origin = @{ x = 0.0; y = 0.0 }
  if ($virtualItems -and $virtualItems.Count -gt 20) { $origin = $virtualItems[20] }
  $list = New-ObjList
  $n = if ($virtualItems) { $virtualItems.Count } else { 0 }
  for ($j = 1; $j -le 20 -and ($j - 1) -lt $n; $j++) {
    $raw = $virtualItems[$j - 1]
    $num = if ($raw.Number) { [int]$raw.Number } else { 50 + $j }
    [void]$list.Add(@{
      Number = $num
      x = [double]$runway.x + [double]$raw.x - [double]$origin.x
      y = [double]$runway.y + [double]$raw.y - [double]$origin.y
    })
  }
  return ,$list
}
function Load-Levels($guidIndex) {
  $configText = [IO.File]::ReadAllText((Join-Path $DataRoot "GameConfig.asset"))
  $blockM = [regex]::Match($configText, "LevelsData:\r?\n([\s\S]*?)(?:\r?\n\w|\r?\n---|$)")
  $levelGuids = if ($blockM.Success) { Get-Guids $blockM.Groups[1].Value } else { New-ObjList }
  $levels = New-ObjList
  for ($i = 0; $i -lt $levelGuids.Count; $i++) {
    $dataPath = $guidIndex[$levelGuids[$i]]
    if (-not $dataPath) { continue }
    $refs = Parse-LevelData $dataPath
    $info = if ($refs.levelInfo -and $guidIndex.ContainsKey($refs.levelInfo)) { Parse-LevelInfo $guidIndex[$refs.levelInfo] } else { @{ LevelNumber = $i; Destination = "?" } }
    $routePts = if ($refs.mainRoute -and $guidIndex.ContainsKey($refs.mainRoute)) { Compute-Cartesian (Parse-Route $guidIndex[$refs.mainRoute]) } else { New-ObjList }
    $atc = if ($refs.aTCs -and $guidIndex.ContainsKey($refs.aTCs)) { Parse-Atc $guidIndex[$refs.aTCs] } else { New-ObjList }
    $vpRaw = if ($refs.virtualPoints -and $guidIndex.ContainsKey($refs.virtualPoints)) { Parse-VirtualPoints $guidIndex[$refs.virtualPoints] } else { New-ObjList }
    [void]$levels.Add(@{
      index = $i; info = $info; routePts = $routePts; atc = $atc
      virtualPts = Compute-VirtualWorld $routePts $vpRaw
    })
  }
  return ,$levels
}
function Resolve-Point($pointId, $routePts, $virtualPts) {
  $id = 0
  [int]::TryParse([string]$pointId, [ref]$id) | Out-Null
  if ($id -lt 50) {
    $p = $null
    foreach ($pt in $routePts) { if ([int]$pt.ID -eq $id) { $p = $pt; break } }
    if ($null -eq $p -and $id -ge 0 -and $id -lt $routePts.Count) { $p = $routePts[$id] }
    if ($null -eq $p) { return @{ id = $id; name = "#$id"; x = 0.0; y = 0.0; kind = "missing" } }
    $nm = if ($p.Name) { [string]$p.Name } else { "#$($p.ID)" }
    return @{ id = [int]$p.ID; name = $nm; x = [double]$p.x; y = [double]$p.y; kind = "route"; starAlt = [string]$p.RawAltitude }
  }
  $v = $null
  foreach ($vp in $virtualPts) { if ([int]$vp.Number -eq $id) { $v = $vp; break } }
  if ($null -eq $v) { return @{ id = $id; name = "V$id"; x = 0.0; y = 0.0; kind = "missing" } }
  return @{ id = [int]$v.Number; name = "V$($v.Number)"; x = [double]$v.x; y = [double]$v.y; kind = "virtual"; starAlt = "" }
}
function Get-AtcMap($atc) {
  $map = @{}
  foreach ($a in $atc) {
    $key = [string]$a.point
    if (-not $map.ContainsKey($key) -or $a.Altitude) { $map[$key] = $a }
  }
  return $map
}
function Group-VirtualPoints($virtualPts) {
  $groups = New-ObjList
  foreach ($v in $virtualPts) {
    $hit = $null
    foreach ($g in $groups) {
      $dx = [double]$g.x - [double]$v.x; $dy = [double]$g.y - [double]$v.y
      if ([Math]::Sqrt($dx * $dx + $dy * $dy) -lt 0.35) { $hit = $g; break }
    }
    if ($hit) { [void]$hit.members.Add($v) }
    else {
      $mem = New-ObjList
      [void]$mem.Add($v)
      [void]$groups.Add(@{ x = [double]$v.x; y = [double]$v.y; members = $mem })
    }
  }
  return ,$groups
}

function New-PdfDoc { return @{ Pages = New-Object System.Collections.Generic.List[object] } }
function Add-PdfPage($doc) {
  $page = @{ W = $PageW; H = $PageH; Sb = New-Object System.Text.StringBuilder }
  [void]$doc.Pages.Add($page)
  return $page
}
function Pdf-N([double]$v) { return ("{0:0.###}" -f $v) }
function Add-Rect($page, [double]$x, [double]$y, [double]$w, [double]$h, $fill, $stroke, [double]$lw = 1) {
  $sb = $page.Sb
  [void]$sb.AppendLine("q")
  if ($fill) { [void]$sb.AppendLine("$($fill[0]) $($fill[1]) $($fill[2]) rg") }
  if ($stroke) { [void]$sb.AppendLine("$($stroke[0]) $($stroke[1]) $($stroke[2]) RG"); [void]$sb.AppendLine("$(Pdf-N $lw) w") }
  [void]$sb.AppendLine("$(Pdf-N $x) $(Pdf-N $y) $(Pdf-N $w) $(Pdf-N $h) re")
  if ($fill -and $stroke) { [void]$sb.AppendLine("B") } elseif ($fill) { [void]$sb.AppendLine("f") } else { [void]$sb.AppendLine("S") }
  [void]$sb.AppendLine("Q")
}
function Add-Line($page, $a, $b, $color, [double]$lw, $dash = $null) {
  $sb = $page.Sb
  [void]$sb.AppendLine("q")
  [void]$sb.AppendLine("$($color[0]) $($color[1]) $($color[2]) RG")
  [void]$sb.AppendLine("$(Pdf-N $lw) w 1 J 1 j")
  if ($dash) { [void]$sb.AppendLine("[$($dash[0]) $($dash[1])] 0 d") }
  [void]$sb.AppendLine("$(Pdf-N $a.x) $(Pdf-N $a.y) m $(Pdf-N $b.x) $(Pdf-N $b.y) l S")
  [void]$sb.AppendLine("Q")
}
function Add-Circle($page, [double]$cx, [double]$cy, [double]$r, $fill, $stroke, [double]$lw = 0.6) {
  $k = 0.5522847498 * $r
  $sb = $page.Sb
  [void]$sb.AppendLine("q")
  if ($fill) { [void]$sb.AppendLine("$($fill[0]) $($fill[1]) $($fill[2]) rg") }
  if ($stroke) { [void]$sb.AppendLine("$($stroke[0]) $($stroke[1]) $($stroke[2]) RG"); [void]$sb.AppendLine("$(Pdf-N $lw) w") }
  [void]$sb.AppendLine("$(Pdf-N $cx) $(Pdf-N ($cy+$r)) m")
  [void]$sb.AppendLine("$(Pdf-N ($cx+$k)) $(Pdf-N ($cy+$r)) $(Pdf-N ($cx+$r)) $(Pdf-N ($cy+$k)) $(Pdf-N ($cx+$r)) $(Pdf-N $cy) c")
  [void]$sb.AppendLine("$(Pdf-N ($cx+$r)) $(Pdf-N ($cy-$k)) $(Pdf-N ($cx+$k)) $(Pdf-N ($cy-$r)) $(Pdf-N $cx) $(Pdf-N ($cy-$r)) c")
  [void]$sb.AppendLine("$(Pdf-N ($cx-$k)) $(Pdf-N ($cy-$r)) $(Pdf-N ($cx-$r)) $(Pdf-N ($cy-$k)) $(Pdf-N ($cx-$r)) $(Pdf-N $cy) c")
  [void]$sb.AppendLine("$(Pdf-N ($cx-$r)) $(Pdf-N ($cy+$k)) $(Pdf-N ($cx-$k)) $(Pdf-N ($cy+$r)) $(Pdf-N $cx) $(Pdf-N ($cy+$r)) c")
  if ($fill -and $stroke) { [void]$sb.AppendLine("B") } elseif ($fill) { [void]$sb.AppendLine("f") } else { [void]$sb.AppendLine("S") }
  [void]$sb.AppendLine("Q")
}
function Add-Diamond($page, [double]$x, [double]$y, [double]$s, $fill, $stroke) {
  $sb = $page.Sb
  [void]$sb.AppendLine("q")
  [void]$sb.AppendLine("$($fill[0]) $($fill[1]) $($fill[2]) rg")
  [void]$sb.AppendLine("$($stroke[0]) $($stroke[1]) $($stroke[2]) RG")
  [void]$sb.AppendLine("0.9 w")
  [void]$sb.AppendLine("$(Pdf-N $x) $(Pdf-N ($y+$s)) m $(Pdf-N ($x+$s)) $(Pdf-N $y) l $(Pdf-N $x) $(Pdf-N ($y-$s)) l $(Pdf-N ($x-$s)) $(Pdf-N $y) l h B")
  [void]$sb.AppendLine("Q")
}
function Add-Text($page, [string]$text, [double]$x, [double]$y, [double]$size, $color, [bool]$bold) {
  $font = if ($bold) { "/F2" } else { "/F1" }
  $sb = $page.Sb
  [void]$sb.AppendLine("BT")
  [void]$sb.AppendLine("$font $(Pdf-N $size) Tf")
  [void]$sb.AppendLine("$($color[0]) $($color[1]) $($color[2]) rg")
  [void]$sb.AppendLine("1 0 0 1 $(Pdf-N $x) $(Pdf-N $y) Tm")
  [void]$sb.AppendLine("($(Escape-Pdf $text)) Tj")
  [void]$sb.AppendLine("ET")
}
function Save-PdfDoc($doc, [string]$path) {
  $objs = New-Object System.Collections.Generic.List[string]
  [void]$objs.Add("")
  $nPages = $doc.Pages.Count
  $kids = @(); for ($i = 0; $i -lt $nPages; $i++) { $kids += "$(5 + $i * 2) 0 R" }
  $objs.Add("<< /Type /Catalog /Pages 2 0 R >>")
  $objs.Add("<< /Type /Pages /Kids [$($kids -join ' ')] /Count $nPages >>")
  $objs.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>")
  $objs.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
  foreach ($p in $doc.Pages) {
    $content = $p.Sb.ToString()
    $bytes = [Text.Encoding]::ASCII.GetBytes($content)
    $contObj = $objs.Count + 1
    $objs.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 $(Pdf-N $p.W) $(Pdf-N $p.H)] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents $contObj 0 R >>")
    $objs.Add("<< /Length $($bytes.Length) >>`nstream`n$content`nendstream")
  }
  $ms = New-Object IO.MemoryStream
  $sw = New-Object IO.StreamWriter($ms, [Text.Encoding]::ASCII, 1024, $true)
  $sw.NewLine = "`n"
  $sw.WriteLine("%PDF-1.4"); $sw.Flush()
  $offsets = @(0)
  for ($i = 1; $i -lt $objs.Count; $i++) {
    $offsets += $ms.Length
    $sw.WriteLine("$i 0 obj"); $sw.WriteLine($objs[$i]); $sw.WriteLine("endobj"); $sw.Flush()
  }
  $xref = $ms.Length
  $sw.WriteLine("xref"); $sw.WriteLine("0 $($objs.Count)"); $sw.WriteLine("0000000000 65535 f ")
  for ($i = 1; $i -lt $objs.Count; $i++) { $sw.WriteLine(("{0:0000000000} 00000 n " -f $offsets[$i])) }
  $sw.WriteLine("trailer"); $sw.WriteLine("<< /Size $($objs.Count) /Root 1 0 R >>")
  $sw.WriteLine("startxref"); $sw.WriteLine("$xref"); $sw.WriteLine("%%EOF"); $sw.Flush()
  $dir = Split-Path $path
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  [IO.File]::WriteAllBytes($path, $ms.ToArray())
  $sw.Dispose(); $ms.Dispose()
}

$C = @{
  bg = @(0.93, 0.94, 0.96); header = @(0.08, 0.14, 0.22); paper = @(1, 1, 1)
  border = @(0.78, 0.82, 0.88); star = @(0.18, 0.42, 0.72); starW = @(0.72, 0.84, 0.94)
  atc = @(0.92, 0.42, 0.08); start = @(0.12, 0.62, 0.32); end = @(0.82, 0.18, 0.18)
  vp = @(0.52, 0.28, 0.72); vpFill = @(0.92, 0.86, 0.98); ink = @(0.12, 0.14, 0.18)
  muted = @(0.4, 0.45, 0.52); gold = @(0.95, 0.78, 0.25); white = @(0.97, 0.98, 0.99)
  leader = @(0.7, 0.72, 0.76); grid = @(0.88, 0.9, 0.93); ils = @(0.82, 0.12, 0.55)
}

function Pick-LabelSlot([double]$px, [double]$py, [double]$boxW, [double]$boxH, $boxes) {
  $candsX = @(6, 6, (-$boxW - 4), (-$boxW - 4), 8, 8, (-$boxW - 6), (-$boxW - 6), 14, (-$boxW - 16), 6, 6)
  $candsY = @(5, (-$boxH - 2), 5, (-$boxH - 2), 16, (-$boxH - 14), 16, (-$boxH - 14), 2, 2, 28, (-$boxH - 26))
  for ($ci = 0; $ci -lt $candsX.Count; $ci++) {
    $bx = $px + [double]$candsX[$ci]
    $by = $py + [double]$candsY[$ci]
    $clash = $false
    foreach ($b in $boxes) {
      if ($bx -lt ([double]$b.x + [double]$b.w) -and ($bx + $boxW) -gt [double]$b.x -and $by -lt ([double]$b.y + [double]$b.h) -and ($by + $boxH) -gt [double]$b.y) {
        $clash = $true; break
      }
    }
    if (-not $clash) { return @{ x = $bx; y = $by; w = $boxW; h = $boxH } }
  }
  return @{ x = $px + 6; y = $py + 5; w = $boxW; h = $boxH }
}

function Project-ToEdge([double]$px, [double]$py, [double]$left, [double]$right, [double]$bottom, [double]$top) {
  $cx = ($left + $right) / 2.0
  $cy = ($bottom + $top) / 2.0
  $dx = $px - $cx
  $dy = $py - $cy
  if ([Math]::Abs($dx) -lt 0.0001 -and [Math]::Abs($dy) -lt 0.0001) {
    return @{ x = $left; y = $cy; edge = "L" }
  }
  $best = $null
  if ($dx -gt 0.0001) {
    $t = ($right - $cx) / $dx
    $y = $cy + $t * $dy
    if ($t -gt 0 -and $y -ge $bottom -and $y -le $top) { $best = @{ t = $t; x = $right; y = $y; edge = "R" } }
  }
  if ($dx -lt -0.0001) {
    $t = ($left - $cx) / $dx
    $y = $cy + $t * $dy
    if ($t -gt 0 -and $y -ge $bottom -and $y -le $top -and ($null -eq $best -or $t -lt $best.t)) {
      $best = @{ t = $t; x = $left; y = $y; edge = "L" }
    }
  }
  if ($dy -gt 0.0001) {
    $t = ($top - $cy) / $dy
    $x = $cx + $t * $dx
    if ($t -gt 0 -and $x -ge $left -and $x -le $right -and ($null -eq $best -or $t -lt $best.t)) {
      $best = @{ t = $t; x = $x; y = $top; edge = "T" }
    }
  }
  if ($dy -lt -0.0001) {
    $t = ($bottom - $cy) / $dy
    $x = $cx + $t * $dx
    if ($t -gt 0 -and $x -ge $left -and $x -le $right -and ($null -eq $best -or $t -lt $best.t)) {
      $best = @{ t = $t; x = $x; y = $bottom; edge = "B" }
    }
  }
  if ($null -eq $best) {
    return @{
      x = [Math]::Max($left, [Math]::Min($right, $px))
      y = [Math]::Max($bottom, [Math]::Min($top, $py))
      edge = "R"
    }
  }
  return $best
}

function Spread-EdgeItems($items, [string]$edge, [double]$left, [double]$right, [double]$bottom, [double]$top) {
  if ($items.Count -le 1) { return }
  $minGap = 24.0
  $sorted = @($items | Sort-Object { if ($edge -eq "L" -or $edge -eq "R") { [double]$_.y } else { [double]$_.x } })
  $lo = if ($edge -eq "L" -or $edge -eq "R") { $bottom + 12 } else { $left + 12 }
  $hi = if ($edge -eq "L" -or $edge -eq "R") { $top - 12 } else { $right - 12 }
  $pos = New-Object "System.Collections.Generic.List[double]"
  foreach ($it in $sorted) {
    if ($edge -eq "L" -or $edge -eq "R") { [void]$pos.Add([double]$it.y) } else { [void]$pos.Add([double]$it.x) }
  }
  for ($i = 0; $i -lt $pos.Count; $i++) {
    if ($pos[$i] -lt $lo) { $pos[$i] = $lo }
    if ($i -gt 0 -and ($pos[$i] - $pos[$i - 1]) -lt $minGap) { $pos[$i] = $pos[$i - 1] + $minGap }
  }
  for ($i = $pos.Count - 1; $i -ge 0; $i--) {
    if ($pos[$i] -gt $hi) { $pos[$i] = $hi }
    if ($i -lt $pos.Count - 1 -and ($pos[$i + 1] - $pos[$i]) -lt $minGap) { $pos[$i] = $pos[$i + 1] - $minGap }
  }
  for ($i = 0; $i -lt $sorted.Count; $i++) {
    if ($edge -eq "L" -or $edge -eq "R") { $sorted[$i].y = $pos[$i] } else { $sorted[$i].x = $pos[$i] }
  }
}

function Draw-Header($page, $level, [int]$pageNo, [int]$pageCount) {
  Add-Rect $page 0 ($PageH - 44) $PageW 44 $C.header $null
  $n = if ($level.info.LevelNumber) { $level.info.LevelNumber } else { $level.index }
  Add-Text $page "LEVEL $n" 16 ($PageH - 28) 20 $C.gold $true
  $title = "$($level.info.Destination)  |  RWY $($level.info.Runway)  |  STAR $($level.info.Star)  |  TRANS $($level.info.Transition)"
  Add-Text $page (Fit-Text $title 12 900 $true) 140 ($PageH - 22) 12 $C.white $true
  $sub = "CRS $($level.info.Course) deg  |  ILS $($level.info.Freq)  |  CRZ $(Format-Alt $level.info.CrzAltitude)  |  page $pageNo/$pageCount"
  Add-Text $page $sub 140 ($PageH - 36) 8 @(0.7, 0.78, 0.88) $false
}

function Draw-Footer($page, $level) {
  Add-Rect $page 10 6 1171 28 $C.paper $C.border
  Add-Text $page "STAR rest.  ATC rest.  magenta = ILS CLR  |  edge VP = outside" 16 16 7 $C.muted $false
  $parts = New-ObjList
  for ($i = 0; $i -lt $level.atc.Count; $i++) {
    $a = $level.atc[$i]
    $r = Resolve-Point $a.point $level.routePts $level.virtualPts
    $bit = [string]$r.name
    $aa = Format-AtcAlt $a
    $as = Format-AtcSpd $a
    if ($aa) { $bit += " $aa" }
    if ($as) { $bit += " $as" }
    if (Test-IsIlsClr $a) { $bit += " ILS CLR" }
    else {
      $ml = Get-ModeLabel $a
      if ($ml -ne "NO CHANGE") { $bit += " $ml" }
    }
    [void]$parts.Add($bit)
  }
  $chain = if ($parts.Count) { "ATC: " + ($parts -join " > ") } else { "" }
  Add-Text $page (Fit-Text $chain 7 900 $false) 250 16 7 $C.ink $false
}

function Draw-Plan($page, $level) {
  $x0 = 10.0; $y0 = 38.0; $w = 1171.0; $h = $PageH - 44 - 40
  Add-Rect $page $x0 $y0 $w $h $C.paper $C.border

  $pts = $level.routePts
  $vps = $level.virtualPts
  if (-not $pts.Count) {
    Add-Text $page "No route data" ($x0 + 40) ($y0 + $h / 2) 12 $C.muted $false
    return
  }
  $vpGroups = Group-VirtualPoints $vps
  $atcMap = Get-AtcMap $level.atc
  $pad = 32.0
  $plotX = $x0 + $pad
  $plotY = $y0 + $pad
  $plotW = $w - $pad * 2
  $plotH = $h - $pad * 2

  $minX = [double]::PositiveInfinity; $maxX = [double]::NegativeInfinity
  $minY = [double]::PositiveInfinity; $maxY = [double]::NegativeInfinity
  foreach ($p in $pts) {
    $px = [double]$p.x; $py = [double]$p.y
    if ($px -lt $minX) { $minX = $px }; if ($px -gt $maxX) { $maxX = $px }
    if ($py -lt $minY) { $minY = $py }; if ($py -gt $maxY) { $maxY = $py }
  }
  $spanX = [Math]::Max($maxX - $minX, 1.0)
  $spanY = [Math]::Max($maxY - $minY, 1.0)
  $scale = [Math]::Min($plotW / $spanX, $plotH / $spanY) * 0.92
  $cx = ($minX + $maxX) / 2.0
  $cy = ($minY + $maxY) / 2.0
  $map = {
    param($p)
    return @{
      x = [double]$plotX + [double]$plotW / 2.0 + ([double]$p.x - [double]$cx) * [double]$scale
      y = [double]$plotY + [double]$plotH / 2.0 + ([double]$p.y - [double]$cy) * [double]$scale
    }
  }

  $left = $plotX + 8
  $right = $plotX + $plotW - 8
  $bottom = $plotY + 8
  $top = $plotY + $plotH - 8

  $posByKey = @{}
  foreach ($p in $pts) {
    $mpt = & $map $p
    $posByKey["R:$($p.ID)"] = @{ x = [double]$mpt.x; y = [double]$mpt.y; outside = $false }
  }

  $edgeL = New-ObjList; $edgeR = New-ObjList; $edgeT = New-ObjList; $edgeB = New-ObjList
  $vpDraw = New-ObjList
  foreach ($g in $vpGroups) {
    $mpt = & $map $g
    $mx = [double]$mpt.x; $my = [double]$mpt.y
    $outside = ($mx -lt $left -or $mx -gt $right -or $my -lt $bottom -or $my -gt $top)
    $item = @{
      group = $g; x = $mx; y = $my; outside = $outside; edge = ""
    }
    if ($outside) {
      $pr = Project-ToEdge $mx $my $left $right $bottom $top
      $item.x = [double]$pr.x; $item.y = [double]$pr.y; $item.edge = [string]$pr.edge
      switch ($item.edge) {
        "L" { [void]$edgeL.Add($item) }
        "R" { [void]$edgeR.Add($item) }
        "T" { [void]$edgeT.Add($item) }
        default { [void]$edgeB.Add($item) }
      }
    }
    [void]$vpDraw.Add($item)
    foreach ($v in $g.members) {
      $posByKey["V:$($v.Number)"] = $item
    }
  }
  Spread-EdgeItems $edgeL "L" $left $right $bottom $top
  Spread-EdgeItems $edgeR "R" $left $right $bottom $top
  Spread-EdgeItems $edgeT "T" $left $right $bottom $top
  Spread-EdgeItems $edgeB "B" $left $right $bottom $top

  Add-Rect $page $plotX $plotY $plotW $plotH $null $C.grid 0.5

  for ($i = 1; $i -lt $pts.Count; $i++) {
    Add-Line $page (& $map $pts[$i - 1]) (& $map $pts[$i]) $C.starW 5.0
  }
  for ($i = 1; $i -lt $pts.Count; $i++) {
    Add-Line $page (& $map $pts[$i - 1]) (& $map $pts[$i]) $C.star 1.8
  }

  $atcPath = New-ObjList
  foreach ($a in $level.atc) {
    $key = if ($a.point -lt 50) { "R:$($a.point)" } else { "V:$($a.point)" }
    $mp = $posByKey[$key]
    if ($null -eq $mp) {
      $r = Resolve-Point $a.point $pts $vps
      $mp = & $map $r
    }
    if ($atcPath.Count -gt 0) {
      $prev = $atcPath[$atcPath.Count - 1]
      $lx = [double]$prev.x - [double]$mp.x
      $ly = [double]$prev.y - [double]$mp.y
      if ([Math]::Sqrt($lx * $lx + $ly * $ly) -lt 0.4) { continue }
    }
    [void]$atcPath.Add(@{ x = [double]$mp.x; y = [double]$mp.y })
  }
  for ($i = 1; $i -lt $atcPath.Count; $i++) {
    Add-Line $page $atcPath[$i - 1] $atcPath[$i] $C.atc 2.3 @(7, 3.5)
  }

  $boxes = New-Object System.Collections.Generic.List[object]
  $drawLabel = {
    param($m, $lineList, $color)
    $size = 7.0
    $wBox = 0.0
    foreach ($t in $lineList) {
      $tw = Get-TextWidth (ConvertTo-Ascii ([string]$t)) $size $false
      if ($tw -gt $wBox) { $wBox = $tw }
    }
    $wBox += 2
    $hBox = ([double]$lineList.Count * 8.5) + 1.0
    $box = Pick-LabelSlot ([double]$m.x) ([double]$m.y) $wBox $hBox $boxes
    [void]$boxes.Add($box)
    Add-Line $page $m @{ x = [double]$box.x + 1; y = [double]$box.y + 2 } $C.leader 0.35
    for ($li = 0; $li -lt $lineList.Count; $li++) {
      Add-Text $page (Fit-Text ([string]$lineList[$li]) $size 110 $false) ([double]$box.x) ([double]$box.y + ($lineList.Count - 1 - $li) * 8.5) $size $color $false
    }
  }

  for ($i = 0; $i -lt $pts.Count; $i++) {
    $p = $pts[$i]
    $mpt = $posByKey["R:$($p.ID)"]
    $isStart = ($i -eq 0)
    $isEnd = ($i -eq $pts.Count - 1)
    $atc = $atcMap[[string]$p.ID]
    $fill = if ($isStart) { $C.start } elseif ($isEnd) { $C.end } else { $C.star }
    $isIls = Test-IsIlsClr $atc
    $stroke = if ($isIls) { $C.ils } elseif ($atc) { $C.atc } else { @(0.1, 0.2, 0.35) }
    $lw = if ($isIls) { 1.8 } elseif ($atc) { 1.3 } else { 0.45 }
    $sz = if ($isIls) { 5.6 } elseif ($isStart -or $isEnd) { 4.4 } else { 2.9 }
    Add-Circle $page $mpt.x $mpt.y $sz $fill $stroke $lw
    if ($isIls) {
      Add-Circle $page $mpt.x $mpt.y 8.2 $null $C.ils 1.4
    }
    $lineList = New-ObjList
    [void]$lineList.Add(("#$($p.ID) $($p.Name)".Trim()))
    $starBits = New-ObjList
    $sa = Format-StarAlt $p.RawAltitude
    $ss = Format-StarSpd $p.RawSpeed
    if ($sa) { [void]$starBits.Add($sa) }
    if ($ss) { [void]$starBits.Add($ss) }
    if ($starBits.Count) { [void]$lineList.Add(($starBits -join " ")) }
    $atcBits = New-ObjList
    $aa = Format-AtcAlt $atc
    $as = Format-AtcSpd $atc
    if ($aa) { [void]$atcBits.Add($aa) }
    if ($as) { [void]$atcBits.Add($as) }
    if ($atcBits.Count) { [void]$lineList.Add("ATC " + ($atcBits -join " ")) }
    if ($isIls) { [void]$lineList.Add("ILS CLR") }
    $lcol = if ($isIls) { $C.ils } elseif ($isEnd) { $C.end } else { $C.ink }
    & $drawLabel $mpt $lineList $lcol
  }

  foreach ($item in $vpDraw) {
    $g = $item.group
    $used = $false
    foreach ($v in $g.members) { if ($atcMap.ContainsKey([string]$v.Number)) { $used = $true; break } }
    $atcItem = $null
    foreach ($v in $g.members) { if ($atcMap.ContainsKey([string]$v.Number)) { $atcItem = $atcMap[[string]$v.Number]; break } }
    $isIls = Test-IsIlsClr $atcItem
    $sz = if ($isIls) { 5.8 } elseif ($item.outside) { 4.6 } elseif ($used) { 4.4 } else { 3.4 }
    $stroke = if ($isIls) { $C.ils } elseif ($used) { $C.atc } else { $C.vp }
    Add-Diamond $page ([double]$item.x) ([double]$item.y) $sz $C.vpFill $stroke
    if ($isIls) {
      Add-Circle $page ([double]$item.x) ([double]$item.y) 8.4 $null $C.ils 1.4
    }
    if ($item.outside) {
      $tick = 7.0
      switch ($item.edge) {
        "L" { Add-Line $page @{ x = [double]$item.x; y = [double]$item.y } @{ x = [double]$item.x - $tick; y = [double]$item.y } $C.vp 1.2 }
        "R" { Add-Line $page @{ x = [double]$item.x; y = [double]$item.y } @{ x = [double]$item.x + $tick; y = [double]$item.y } $C.vp 1.2 }
        "T" { Add-Line $page @{ x = [double]$item.x; y = [double]$item.y } @{ x = [double]$item.x; y = [double]$item.y + $tick } $C.vp 1.2 }
        default { Add-Line $page @{ x = [double]$item.x; y = [double]$item.y } @{ x = [double]$item.x; y = [double]$item.y - $tick } $C.vp 1.2 }
      }
    }
    $nums = @($g.members | ForEach-Object { $_.Number } | Sort-Object)
    $name = if ($nums.Count -le 3) { ($nums | ForEach-Object { "V$_" }) -join "/" } else { "V$($nums[0])-$($nums[-1]) ($($nums.Count))" }
    $lineList = New-ObjList
    [void]$lineList.Add($name)
    if ($item.outside) { [void]$lineList.Add("out") }
    $atcBits = New-ObjList
    $aa = Format-AtcAlt $atcItem
    $as = Format-AtcSpd $atcItem
    if ($aa) { [void]$atcBits.Add($aa) }
    if ($as) { [void]$atcBits.Add($as) }
    if ($atcBits.Count) { [void]$lineList.Add("ATC " + ($atcBits -join " ")) }
    if ($isIls) { [void]$lineList.Add("ILS CLR") }
    elseif ($atcItem) {
      $ml = Get-ModeLabel $atcItem
      if ($ml -ne "NO CHANGE") { [void]$lineList.Add($ml) }
    }
    $lcol = if ($isIls) { $C.ils } else { $C.vp }
    & $drawLabel @{ x = [double]$item.x; y = [double]$item.y } $lineList $lcol
  }

  $nm = 10.0
  $bar = $nm * $scale
  if ($bar -gt 16 -and $bar -lt ($plotW * 0.35)) {
    $bx = $plotX + 12; $by = $plotY + 12
    Add-Line $page @{ x = $bx; y = $by } @{ x = $bx + $bar; y = $by } $C.ink 1.5
    Add-Line $page @{ x = $bx; y = $by - 3 } @{ x = $bx; y = $by + 3 } $C.ink 1
    Add-Line $page @{ x = $bx + $bar; y = $by - 3 } @{ x = $bx + $bar; y = $by + 3 } $C.ink 1
    Add-Text $page "$nm NM" ($bx + 4) ($by + 6) 7 $C.muted $false
  }
  Add-Text $page "N" ($plotX + $plotW - 18) ($plotY + $plotH - 16) 9 $C.ink $true
  Add-Line $page @{ x = $plotX + $plotW - 14; y = $plotY + $plotH - 26 } @{ x = $plotX + $plotW - 14; y = $plotY + $plotH - 8 } $C.ink 1.2
}

Write-Host "Indexing assets..."
$guidIndex = Build-GuidIndex
Write-Host "Loading levels..."
$all = Load-Levels $guidIndex
$levels = New-ObjList
foreach ($lv in $all) {
  if ($lv.index -ge 1 -and $lv.index -le 39) { [void]$levels.Add($lv) }
}
Write-Host ("Drawing {0} pages..." -f $levels.Count)

$pdf = New-PdfDoc
for ($i = 0; $i -lt $levels.Count; $i++) {
  $level = $levels[$i]
  $page = Add-PdfPage $pdf
  Add-Rect $page 0 0 $PageW $PageH $C.bg $null
  Draw-Header $page $level ($i + 1) $levels.Count
  Draw-Plan $page $level
  Draw-Footer $page $level
  $n = if ($level.info.LevelNumber) { $level.info.LevelNumber } else { $level.index }
  Write-Host ("  Level {0}: {1}  pts {2}  VP {3}  ATC {4}" -f $n, $level.info.Destination, $level.routePts.Count, $level.virtualPts.Count, $level.atc.Count)
}

Save-PdfDoc $pdf $OutPdf
$fi = Get-Item $OutPdf
Write-Host ("Wrote {0} ({1} bytes, {2} pages)" -f $OutPdf, $fi.Length, $levels.Count)
