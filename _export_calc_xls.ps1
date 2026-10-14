$ErrorActionPreference = "Stop"
$path = "C:\Users\User\NavigationShare\CalculatorTablolari.xlsx"
if (Test-Path $path) { Remove-Item $path -Force }

function RGB([int]$r, [int]$g, [int]$b) { return $r + ($g * 256) + ($b * 65536) }

$alts9 = @("FL400","FL350","FL300","FL250","FL200","FL150","FL100","FL50","Deniz")
$alts5 = @("FL200","FL150","FL100","FL50","Deniz")
$flapNames = @("Temiz","Flap 1","Flap 2","Flap 5","Flap 10","Flap 15","Flap 25","Flap 30","Flap 40")
$cornerNames = @(
    "Yuksek hiz, duz ucus  |  ilk sutun hiz (kt)",
    "Yuksek hiz, idle  |  ilk sutun dikey hiz (fpm)",
    "Dusuk hiz, duz ucus  |  ilk sutun hiz (kt)",
    "Dusuk hiz, idle  |  ilk sutun dikey hiz (fpm)"
)

# Each altitude is 4 corners x 4 raw fields: speed-or-VS, pitch, N1, FF.
$M = @(
    "240,300,9090,240|-2000,100,4780,48|200,600,8690,212|-1700,300,4620,48",
    "280,200,9200,306|-2500,-100,4640,60|220,400,8060,220|-1600,300,4320,60",
    "280,200,8340,330|-2400,-100,4540,74|220,400,7770,240|-1500,300,4060,74",
    "280,200,8010,348|-2200,-100,4000,88|220,400,7520,258|-1500,300,3710,88",
    "280,200,7820,365|-2000,-100,3850,100|220,400,7180,275|-1300,200,3530,100",
    "280,200,7510,378|-1900,-100,3700,114|220,400,6710,290|-1200,300,3420,114",
    "280,300,7100,390|-1800,-100,3570,126|220,400,6440,305|-1200,300,3300,126",
    "280,200,6800,400|-1700,0,3460,136|220,500,6080,318|-1200,200,3200,136",
    "280,200,6530,410|-1500,0,3340,146|220,450,5550,330|-1300,100,3120,146"
)
$Mf = @(
    @(
        "280,200,7820,365|-2000,-100,3850,100|220,400,7180,275|-1300,200,3530,100",
        "280,200,7510,378|-1900,-100,3700,114|220,400,6710,290|-1200,300,3420,114",
        "280,300,7100,390|-1800,-100,3570,126|220,400,6440,305|-1200,300,3300,126",
        "280,200,6800,400|-1700,0,3460,136|220,500,6080,318|-1200,200,3200,136",
        "280,200,6530,410|-1500,0,3340,146|220,450,5550,330|-1300,100,3120,146"
    ),
    @(
        "240,200,7110,383|-1400,100,3670,116|180,600,6600,291|-1200,400,3370,116",
        "240,200,7110,396|-1400,100,3670,132|180,600,6600,306|-1200,400,3370,132",
        "240,200,6700,408|-1400,100,3350,148|180,500,6220,321|-1200,400,3250,148",
        "240,200,6420,418|-1400,0,3240,162|180,600,5990,334|-1200,400,3150,162",
        "240,200,6150,428|-1400,0,3140,176|180,400,5640,346|-1200,400,3060,176"
    ),
    @(
        "240,100,7250,397|-1600,0,3670,124|180,500,6580,305|-1300,300,3370,124",
        "240,100,7250,410|-1600,0,3670,142|180,500,6580,320|-1300,300,3370,142",
        "240,100,6800,422|-1600,0,3350,158|180,400,6340,335|-1300,300,3250,158",
        "240,100,6540,432|-1600,0,3240,172|180,400,5970,348|-1300,300,3150,172",
        "240,100,6270,442|-1600,0,3140,186|180,150,5630,360|-1300,300,3060,186"
    ),
    @(
        "240,0,7610,402|-1800,0,3670,132|180,400,6850,318|-1400,300,3370,132",
        "240,0,7610,418|-1800,0,3670,150|180,400,6850,336|-1400,300,3370,150",
        "240,0,7140,425|-1800,-100,3350,166|180,400,6480,347|-1400,250,3250,166",
        "240,0,6760,435|-1800,-100,3240,180|180,400,6190,357|-1400,250,3150,180",
        "240,0,6600,450|-1800,-100,3140,194|180,400,5920,365|-1400,250,3060,194"
    ),
    @(
        "200,200,7500,412|-1800,0,3670,138|160,500,7210,366|-1500,400,3370,138",
        "200,200,7500,428|-1800,0,3670,156|160,500,7210,384|-1500,400,3370,156",
        "200,200,7010,441|-1800,0,3350,172|160,500,6480,395|-1500,250,3250,172",
        "200,200,6650,451|-1800,0,3240,186|160,500,6190,431|-1500,250,3150,186",
        "200,200,6510,470|-1800,0,3140,200|160,600,5920,428|-1500,250,3060,200"
    ),
    @(
        "190,200,7780,457|-1300,0,3670,144|150,600,7700,402|-1000,500,3370,144",
        "190,200,7780,473|-1300,0,3670,162|150,600,7700,420|-1000,500,3370,162",
        "190,200,7260,476|-1300,0,3350,178|150,600,7000,431|-1000,500,3250,178",
        "190,200,6850,498|-1300,0,3240,192|150,700,6600,452|-1000,500,3150,192",
        "190,200,6600,494|-1300,0,3140,204|150,700,6380,466|-1000,500,3060,204"
    ),
    @(
        "180,100,8070,529|-1600,-100,3670,150|140,600,7870,464|-1300,500,3370,150",
        "180,100,8070,545|-1600,-100,3670,168|140,600,7870,482|-1300,500,3370,168",
        "180,100,7810,560|-1600,-100,3350,184|140,600,7580,495|-1300,500,3250,184",
        "180,100,7460,563|-1600,-100,3240,196|140,600,6790,500|-1300,400,3150,196",
        "180,100,6950,577|-1600,-100,3140,206|140,600,6580,515|-1300,400,3060,206"
    ),
    @(
        "170,-100,8150,552|-1800,-200,3670,154|130,600,7940,498|-1200,500,3370,154",
        "170,-100,8150,568|-1800,-200,3670,172|130,600,7940,516|-1200,500,3370,172",
        "170,-100,7950,590|-1800,-200,3350,188|130,600,7800,538|-1200,400,3250,188",
        "170,-100,7660,600|-1800,-200,3240,200|130,600,7170,561|-1200,400,3150,200",
        "170,-100,6950,589|-1800,-200,3140,208|130,600,6760,565|-1200,400,3060,208"
    ),
    @(
        "160,0,8410,603|-1900,-200,3670,158|130,500,8110,548|-1400,300,3370,158",
        "160,0,8410,619|-1900,-200,3670,176|130,500,8110,566|-1400,300,3370,176",
        "160,0,8050,623|-1900,-200,3350,192|130,500,7920,580|-1400,300,3250,192",
        "160,0,7940,655|-1900,-200,3240,204|130,500,7510,593|-1400,300,3150,204",
        "160,-100,7300,644|-1900,-200,3140,210|130,500,6920,598|-1400,300,3060,210"
    )
)

function Parse-Row([string]$line) {
    $corners = @()
    foreach ($part in $line.Split("|")) {
        $nums = @()
        foreach ($n in $part.Split(",")) { $nums += [double]$n }
        $corners += ,$nums
    }
    return ,$corners
}

function To-Pilot([int]$corner, [int]$param, [double]$raw) {
    if ($param -eq 0) { return $raw }
    if ($param -eq 1) { return [math]::Round($raw / 100.0, 2) }
    if ($param -eq 2) { return [math]::Round($raw / 100.0, 2) }
    return [double]($raw * 10.0)
}

function Add-DeviationColors($range) {
    $addr = $range.Cells.Item(1, 1).Address($false, $false)
    $rules = @(
        @{ F = "=AND(ISNUMBER($addr),ABS($addr)<=0.02)"; Fill = (RGB 198 239 206); Font = (RGB 0 97 0) },
        @{ F = "=AND(ISNUMBER($addr),ABS($addr)<=0.05)"; Fill = (RGB 255 242 204); Font = (RGB 128 96 0) },
        @{ F = "=AND(ISNUMBER($addr),ABS($addr)<=0.10)"; Fill = (RGB 252 228 214); Font = (RGB 192 80 0) },
        @{ F = "=AND(ISNUMBER($addr),ABS($addr)<=0.20)"; Fill = (RGB 248 203 173); Font = (RGB 192 0 0) },
        @{ F = "=AND(ISNUMBER($addr),ABS($addr)>0.20)"; Fill = (RGB 255 107 107); Font = (RGB 156 0 6) }
    )
    foreach ($rule in $rules) {
        $fc = $range.FormatConditions.Add(2, $null, $rule.F)
        $fc.StopIfTrue = $true
        $fc.Interior.Color = $rule.Fill
        $fc.Font.Color = $rule.Font
    }
}

function Write-PerfSheet($ws, $leadCols, $blocks) {
    # blocks: list of @{ Title; Names; Rows } where Rows[alt][corner][param]
    $ws.Cells.Item(1, 1) = "Calculator performans tablosu"
    $ws.Cells.Item(2, 1) = "Referans: GW 56.4 t, ISA, packs acik, anti-ice kapali, iki motor. Sari kutu gercek ucak degeri icindir. Ayni birimde yaz. Fark = sim - gercek. Sapma bos kutuda renksiz kalir."
    $ws.Range($ws.Cells.Item(1, 1), $ws.Cells.Item(1, 17 + $leadCols)).Merge() | Out-Null
    $ws.Range($ws.Cells.Item(2, 1), $ws.Cells.Item(2, 17 + $leadCols)).Merge() | Out-Null
    $ws.Rows.Item(1).Font.Bold = $true
    $ws.Rows.Item(1).Font.Size = 16
    $ws.Rows.Item(2).Font.Size = 10
    $ws.Rows.Item(2).RowHeight = 32
    $ws.Rows.Item(2).WrapText = $true

    $headerRow = 4
    $groupRow = 4
    $nameRow = 5
    $unitRow = 6
    $groups = @(
        @{ Name = "Hiz / dikey hiz"; Unit = "kt veya fpm" },
        @{ Name = "Pitch"; Unit = "derece" },
        @{ Name = "N1"; Unit = "%" },
        @{ Name = "Yakit akisi"; Unit = "lb/saat, motor basi" }
    )
    $base = 1 + $leadCols
    if ($leadCols -ge 1) { $ws.Cells.Item($nameRow, 1) = "Flap" }
    if ($leadCols -ge 2) { $ws.Cells.Item($nameRow, 2) = "Irtifa" }
    if ($leadCols -eq 1) { $ws.Cells.Item($nameRow, 1) = "Irtifa" }
    for ($g = 0; $g -lt 4; $g++) {
        $c0 = $base + ($g * 4)
        $ws.Range($ws.Cells.Item($groupRow, $c0), $ws.Cells.Item($groupRow, $c0 + 3)).Merge() | Out-Null
        $ws.Cells.Item($groupRow, $c0) = $groups[$g].Name
        $ws.Cells.Item($nameRow, $c0) = "Sim"
        $ws.Cells.Item($nameRow, $c0 + 1) = "Gercek ucak"
        $ws.Cells.Item($nameRow, $c0 + 2) = "Fark"
        $ws.Cells.Item($nameRow, $c0 + 3) = "Sapma"
        $ws.Cells.Item($unitRow, $c0) = $groups[$g].Unit
        $ws.Cells.Item($unitRow, $c0 + 1) = "ayni birim"
        $ws.Cells.Item($unitRow, $c0 + 2) = "sim - gercek"
        $ws.Cells.Item($unitRow, $c0 + 3) = "yuzde"
    }
    $lastCol = $base + 15
    $head = $ws.Range($ws.Cells.Item($groupRow, 1), $ws.Cells.Item($unitRow, $lastCol))
    $head.Font.Bold = $true
    $head.Interior.Color = (RGB 31 78 121)
    $head.Font.Color = (RGB 255 255 255)
    $head.HorizontalAlignment = -4108

    $row = 8
    $dataStart = 8
    foreach ($block in $blocks) {
        $ws.Range($ws.Cells.Item($row, 1), $ws.Cells.Item($row, $lastCol)).Merge() | Out-Null
        $ws.Cells.Item($row, 1) = $block.Title
        $ws.Cells.Item($row, 1).Font.Bold = $true
        $ws.Cells.Item($row, 1).Font.Color = (RGB 255 255 255)
        $ws.Range($ws.Cells.Item($row, 1), $ws.Cells.Item($row, $lastCol)).Interior.Color = (RGB 47 84 150)
        $row++
        $parsed = @()
        foreach ($line in $block.Lines) { $parsed += ,(Parse-Row $line) }
        for ($a = 0; $a -lt $block.Names.Count; $a++) {
            $col = 1
            if ($leadCols -ge 2) {
                $ws.Cells.Item($row, 1) = $block.Flap
                $ws.Cells.Item($row, 2) = $block.Names[$a]
                $col = 3
            } else {
                $ws.Cells.Item($row, 1) = $block.Names[$a]
                $col = 2
            }
            for ($p = 0; $p -lt 4; $p++) {
                $simCol = $col
                $realCol = $col + 1
                $diffCol = $col + 2
                $pctCol = $col + 3
                $sim = To-Pilot $block.Corner $p $parsed[$a][$block.Corner][$p]
                $ws.Cells.Item($row, $simCol) = $sim
                $simAddr = $ws.Cells.Item($row, $simCol).Address($false, $false)
                $realAddr = $ws.Cells.Item($row, $realCol).Address($false, $false)
                $ws.Cells.Item($row, $diffCol).Formula = "=IF($realAddr=`"`",`"`",$simAddr-$realAddr)"
                $ws.Cells.Item($row, $pctCol).Formula = "=IF($realAddr=`"`",`"`",IF($realAddr=0,IF($simAddr=0,0,SIGN($simAddr-$realAddr)),($simAddr-$realAddr)/ABS($realAddr)))"
                $ws.Cells.Item($row, $realCol).Interior.Color = (RGB 255 242 204)
                $ws.Cells.Item($row, $realCol).Borders.LineStyle = 1
                $ws.Cells.Item($row, $realCol).Borders.Color = (RGB 191 144 0)
                $fmt = @("0", "0.00", "0.00", "#,##0")[$p]
                $ws.Cells.Item($row, $simCol).NumberFormat = $fmt
                $ws.Cells.Item($row, $realCol).NumberFormat = $fmt
                $ws.Cells.Item($row, $diffCol).NumberFormat = $fmt
                $ws.Cells.Item($row, $pctCol).NumberFormat = "0.0%"
                $col += 4
            }
            $row++
        }
        $row++
    }
    $dataEnd = $row - 2

    for ($g = 0; $g -lt 4; $g++) {
        $c0 = $base + ($g * 4)
        $pctRange = $ws.Range($ws.Cells.Item($dataStart, $c0 + 3), $ws.Cells.Item($dataEnd, $c0 + 3))
        Add-DeviationColors $pctRange
    }

    $ws.Columns.Item(1).ColumnWidth = 28
    if ($leadCols -ge 2) { $ws.Columns.Item(2).ColumnWidth = 12 }
    for ($c = $base; $c -le $lastCol; $c++) { $ws.Columns.Item($c).ColumnWidth = 14 }
    $ws.Activate() | Out-Null
    $ws.Application.ActiveWindow.SplitRow = 6
    $ws.Application.ActiveWindow.FreezePanes = $true
    $ws.Rows.Item(2).WrapText = $true
    $ws.PageSetup.Orientation = 2
    $ws.PageSetup.Zoom = $false
    $ws.PageSetup.FitToPagesWide = 1
    $ws.PageSetup.FitToPagesTall = $false
}

function Isa-Delta([double]$ft) {
    $h = $ft * 0.3048
    $trop = 11000.0
    $T0 = 288.15
    $L = 0.0065
    $g = 9.80665
    $R = 287.05287
    if ($h -le $trop) {
        $T = $T0 - $L * $h
        return [math]::Pow($T / $T0, $g / ($R * $L))
    }
    $Ttrop = $T0 - $L * $trop
    $ptrop = [math]::Pow($Ttrop / $T0, $g / ($R * $L))
    return $ptrop * [math]::Exp(-$g * ($h - $trop) / ($R * $Ttrop))
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$excel.ScreenUpdating = $false
$wb = $excel.Workbooks.Add()
while ($wb.Worksheets.Count -lt 4) { $wb.Worksheets.Add() | Out-Null }
$wsInfo = $wb.Worksheets.Item(1)
$wsClean = $wb.Worksheets.Item(2)
$wsFlap = $wb.Worksheets.Item(3)
$wsExtra = $wb.Worksheets.Item(4)
$wsInfo.Name = "Okuma"
$wsClean.Name = "Temiz"
$wsFlap.Name = "Flap"
$wsExtra.Name = "Ek tablolar"

$cleanBlocks = @()
for ($c = 0; $c -lt 4; $c++) {
    $cleanBlocks += @{ Title = $cornerNames[$c]; Names = $alts9; Lines = $M; Corner = $c; Flap = "" }
}
Write-PerfSheet $wsClean 1 $cleanBlocks

$flapBlocks = @()
for ($f = 0; $f -lt 9; $f++) {
    for ($c = 0; $c -lt 4; $c++) {
        $flapBlocks += @{ Title = ($flapNames[$f] + "  —  " + $cornerNames[$c]); Names = $alts5; Lines = $Mf[$f]; Corner = $c; Flap = $flapNames[$f] }
    }
}
Write-PerfSheet $wsFlap 2 $flapBlocks

# Extra tables: one row per field, yellow real box, deviation formula.
function Write-ExtraHeader() {
    $wsExtra.Cells.Item(1, 1) = "Ek calculator tablolari"
    $wsExtra.Cells.Item(2, 1) = "Sari kutu gercek ucak degeri. Basinc oraninda ISA degeri dolduruldu; sapma rengi orada gorunur. Diger kutular bos birakildi."
    $wsExtra.Range("A1:H1").Merge() | Out-Null
    $wsExtra.Range("A2:H2").Merge() | Out-Null
    $wsExtra.Rows.Item(1).Font.Bold = $true
    $wsExtra.Rows.Item(1).Font.Size = 16
    $headers = @("Tablo","Irtifa","Alan","Birim","Sim","Gercek ucak","Fark","Sapma")
    for ($i = 0; $i -lt $headers.Count; $i++) { $wsExtra.Cells.Item(4, $i + 1) = $headers[$i] }
    $head = $wsExtra.Range("A4:H4")
    $head.Font.Bold = $true
    $head.Font.Color = (RGB 255 255 255)
    $head.Interior.Color = (RGB 31 78 121)
}
Write-ExtraHeader

$extra = New-Object System.Collections.Generic.List[object]
$pressure = @(0.1852, 0.2352, 0.2968, 0.3709, 0.4594, 0.5642, 0.6856, 0.8320, 1.0)
$feet = @(40000, 35000, 30000, 25000, 20000, 15000, 10000, 5000, 0)
$anti = @(12, 14, 16, 18, 22, 26, 30, 35, 40)
$gearVs = @(
    @(-2060, -1190), @(-2920, -1420), @(-2650, -1290), @(-2430, -1180), @(-2220, -1080),
    @(-2050, -990), @(-1890, -920), @(-1750, -850), @(-1620, -790)
)
$sbVs = @(
    @(-980, -570), @(-1390, -670), @(-1260, -610), @(-1160, -560), @(-1060, -510),
    @(-980, -470), @(-900, -440), @(-830, -400), @(-770, -380)
)
$mvs = @(
    @(-800, -280), @(-680, -520), @(-500, -400), @(-430, -140), @(-420, -240)
)
$gearOn = @(
    @(43.00, 1400, 41.70, 1440),
    @(41.50, 1600, 41.20, 1640),
    @(41.00, 1760, 40.70, 1800),
    @(40.70, 1900, 40.50, 1960)
)
$gearOffN1 = @(
    @(37.00, 34.20),
    @(33.70, 33.00),
    @(34.60, 32.00),
    @(33.40, 31.20)
)
$gearOffFf = @(1140, 1260, 1360, 1460)
$flapMin = @(206, 187, 187, 167, 167, 157, 145, 145, 145)
$flapZ = @(-179, -145, -103, -64, -35, -3, 29, 57, 86)
$lowAlts = @("FL150","FL100","FL50","Deniz")

for ($i = 0; $i -lt 9; $i++) {
    $extra.Add(@("Basinc orani", $alts9[$i], "delta", "oran", $pressure[$i], (Isa-Delta $feet[$i]), "0.0000"))
    $extra.Add(@("Motor anti-ice ek yakit", $alts9[$i], "FF ek", "lb/saat", ($anti[$i] * 10), $null, "#,##0"))
    $extra.Add(@("Inis takimi ek idle VS", $alts9[$i], "Yuksek hiz idle", "fpm", $gearVs[$i][0], $null, "0"))
    $extra.Add(@("Inis takimi ek idle VS", $alts9[$i], "Dusuk hiz idle", "fpm", $gearVs[$i][1], $null, "0"))
    $extra.Add(@("Speedbrake ek idle VS", $alts9[$i], "Yuksek hiz idle", "fpm", $sbVs[$i][0], $null, "0"))
    $extra.Add(@("Speedbrake ek idle VS", $alts9[$i], "Dusuk hiz idle", "fpm", $sbVs[$i][1], $null, "0"))
    $extra.Add(@("Speedbrake ek N1", $alts9[$i], "Duz ucus yuksek hiz", "%", 7.5, $null, "0.00"))
    $extra.Add(@("Speedbrake ek N1", $alts9[$i], "Duz ucus dusuk hiz", "%", 4.5, $null, "0.00"))
    $hiFf = [math]::Round(115 * $pressure[$i] * 10)
    $loFf = [math]::Round(80 * $pressure[$i] * 10)
    $extra.Add(@("Speedbrake ek FF", $alts9[$i], "Duz ucus yuksek hiz", "lb/saat", $hiFf, $null, "#,##0"))
    $extra.Add(@("Speedbrake ek FF", $alts9[$i], "Duz ucus dusuk hiz", "lb/saat", $loFf, $null, "#,##0"))
}
for ($i = 0; $i -lt 4; $i++) {
    $extra.Add(@("Gear acik idle", $lowAlts[$i], "Yuksek hiz N1", "%", $gearOn[$i][0], $null, "0.00"))
    $extra.Add(@("Gear acik idle", $lowAlts[$i], "Yuksek hiz FF", "lb/saat", $gearOn[$i][1], $null, "#,##0"))
    $extra.Add(@("Gear acik idle", $lowAlts[$i], "Dusuk hiz N1", "%", $gearOn[$i][2], $null, "0.00"))
    $extra.Add(@("Gear acik idle", $lowAlts[$i], "Dusuk hiz FF", "lb/saat", $gearOn[$i][3], $null, "#,##0"))
    $extra.Add(@("Gear kapali idle", $lowAlts[$i], "Yuksek hiz N1", "%", $gearOffN1[$i][0], $null, "0.00"))
    $extra.Add(@("Gear kapali idle", $lowAlts[$i], "Dusuk hiz N1", "%", $gearOffN1[$i][1], $null, "0.00"))
    $extra.Add(@("Gear kapali idle", $lowAlts[$i], "FF her iki kose", "lb/saat", $gearOffFf[$i], $null, "#,##0"))
}
for ($i = 0; $i -lt 5; $i++) {
    $extra.Add(@("Mach gecisi ek idle VS", $alts9[$i], "Yuksek hiz idle", "fpm", $mvs[$i][0], $null, "0"))
    $extra.Add(@("Mach gecisi ek idle VS", $alts9[$i], "Dusuk hiz idle", "fpm", $mvs[$i][1], $null, "0"))
}
for ($i = 0; $i -lt 9; $i++) {
    $extra.Add(@("Flap minimum surat", $flapNames[$i], "Secilebilir taban", "kt", $flapMin[$i], $null, "0"))
    $extra.Add(@("Flap kolu ekran acisi", $flapNames[$i], "3D kol Z", "derece", $flapZ[$i], $null, "0"))
}

$r = 5
foreach ($item in $extra) {
    $wsExtra.Cells.Item($r, 1) = $item[0]
    $wsExtra.Cells.Item($r, 2) = $item[1]
    $wsExtra.Cells.Item($r, 3) = $item[2]
    $wsExtra.Cells.Item($r, 4) = $item[3]
    $wsExtra.Cells.Item($r, 5) = $item[4]
    if ($null -ne $item[5]) { $wsExtra.Cells.Item($r, 6) = $item[5] }
    $wsExtra.Cells.Item($r, 7).Formula = "=IF(F$r=`"`",`"`",E$r-F$r)"
    $wsExtra.Cells.Item($r, 8).Formula = "=IF(F$r=`"`",`"`",IF(F$r=0,IF(E$r=0,0,SIGN(E$r-F$r)),(E$r-F$r)/ABS(F$r)))"
    $wsExtra.Cells.Item($r, 5).NumberFormat = $item[6]
    $wsExtra.Cells.Item($r, 6).NumberFormat = $item[6]
    $wsExtra.Cells.Item($r, 7).NumberFormat = $item[6]
    $wsExtra.Cells.Item($r, 6).Interior.Color = (RGB 255 242 204)
    $wsExtra.Cells.Item($r, 6).Borders.LineStyle = 1
    $wsExtra.Cells.Item($r, 6).Borders.Color = (RGB 191 144 0)
    $r++
}
$lastExtra = $r - 1
$wsExtra.Range("H5:H$lastExtra").NumberFormat = "0.0%"
Add-DeviationColors $wsExtra.Range("H5:H$lastExtra")
$wsExtra.Columns.Item(1).ColumnWidth = 28
$wsExtra.Columns.Item(2).ColumnWidth = 14
$wsExtra.Columns.Item(3).ColumnWidth = 22
$wsExtra.Columns.Item(4).ColumnWidth = 18
$wsExtra.Columns.Item(5).ColumnWidth = 14
$wsExtra.Columns.Item(6).ColumnWidth = 16
$wsExtra.Columns.Item(7).ColumnWidth = 14
$wsExtra.Columns.Item(8).ColumnWidth = 12
$wsExtra.Activate() | Out-Null
$wsExtra.Application.ActiveWindow.SplitRow = 4
$wsExtra.Application.ActiveWindow.FreezePanes = $true

# Reading sheet with the color legend and a live sample.
$wsInfo.Cells.Item(1, 1) = "Calculator tablolari"
$wsInfo.Cells.Item(2, 1) = "Temiz, Flap ve Ek sayfalarda her sim degerinin yaninda sari bir kutu var. Gercek ucak degerini ayni birimde o kutuya yaz. Sapma hucresi fark buyudukce yesilden kirmiziya gider."
$wsInfo.Range("A1:F1").Merge() | Out-Null
$wsInfo.Range("A2:F2").Merge() | Out-Null
$wsInfo.Rows.Item(1).Font.Bold = $true
$wsInfo.Rows.Item(1).Font.Size = 18
$wsInfo.Rows.Item(2).RowHeight = 36
$wsInfo.Rows.Item(2).WrapText = $true

$wsInfo.Cells.Item(4, 1) = "Birimler"
$wsInfo.Cells.Item(5, 1) = "Hiz"
$wsInfo.Cells.Item(5, 2) = "knot. Duz ucus koselerinde."
$wsInfo.Cells.Item(6, 1) = "Dikey hiz"
$wsInfo.Cells.Item(6, 2) = "fpm. Idle koselerinin ilk sutunu. Eksi deger alcalma."
$wsInfo.Cells.Item(7, 1) = "Pitch"
$wsInfo.Cells.Item(7, 2) = "derece. Tablodaki ham deger 100'e bolunur (300 = 3.00 derece)."
$wsInfo.Cells.Item(8, 1) = "N1"
$wsInfo.Cells.Item(8, 2) = "yuzde. Ham deger 100'e bolunur (9090 = %90.90)."
$wsInfo.Cells.Item(9, 1) = "Yakit"
$wsInfo.Cells.Item(9, 2) = "lb/saat, motor basina. Ham deger 10 ile carpilir (240 = 2400 lb/saat, gostergede 2.40)."
$wsInfo.Cells.Item(10, 1) = "Fark"
$wsInfo.Cells.Item(10, 2) = "Sim eksi gercek. Pozitif ise sim daha buyuk."
$wsInfo.Cells.Item(11, 1) = "Sapma"
$wsInfo.Cells.Item(11, 2) = "Fark / gercek degerin mutlak degeri."

$wsInfo.Cells.Item(13, 1) = "Renk"
$wsInfo.Cells.Item(13, 2) = "Sapma"
$legend = @(
    @{ T = "yuzde 2 ve alti"; V = 0.01; Fill = (RGB 198 239 206); Font = (RGB 0 97 0) },
    @{ T = "yuzde 2 - 5"; V = 0.04; Fill = (RGB 255 242 204); Font = (RGB 128 96 0) },
    @{ T = "yuzde 5 - 10"; V = 0.08; Fill = (RGB 252 228 214); Font = (RGB 192 80 0) },
    @{ T = "yuzde 10 - 20"; V = 0.15; Fill = (RGB 248 203 173); Font = (RGB 192 0 0) },
    @{ T = "yuzde 20 ustu"; V = 0.35; Fill = (RGB 255 107 107); Font = (RGB 156 0 6) }
)
for ($i = 0; $i -lt $legend.Count; $i++) {
    $rr = 14 + $i
    $wsInfo.Cells.Item($rr, 1) = $legend[$i].T
    $wsInfo.Cells.Item($rr, 2) = $legend[$i].V
    $wsInfo.Cells.Item($rr, 2).NumberFormat = "0.0%"
    $wsInfo.Cells.Item($rr, 2).Interior.Color = $legend[$i].Fill
    $wsInfo.Cells.Item($rr, 2).Font.Color = $legend[$i].Font
    $wsInfo.Cells.Item($rr, 2).Font.Bold = $true
}

$wsInfo.Cells.Item(20, 1) = "Ornek"
$wsInfo.Cells.Item(21, 1) = "Sim"
$wsInfo.Cells.Item(21, 2) = "Gercek ucak"
$wsInfo.Cells.Item(21, 3) = "Fark"
$wsInfo.Cells.Item(21, 4) = "Sapma"
$wsInfo.Range("A21:D21").Font.Bold = $true
$wsInfo.Range("A21:D21").Font.Color = (RGB 255 255 255)
$wsInfo.Range("A21:D21").Interior.Color = (RGB 31 78 121)
$wsInfo.Cells.Item(22, 1) = 4100
$wsInfo.Cells.Item(22, 2) = 3500
$wsInfo.Cells.Item(22, 3).Formula = "=A22-B22"
$wsInfo.Cells.Item(22, 4).Formula = "=(A22-B22)/ABS(B22)"
$wsInfo.Cells.Item(22, 2).Interior.Color = (RGB 255 242 204)
$wsInfo.Cells.Item(22, 2).Borders.LineStyle = 1
$wsInfo.Range("A22:C22").NumberFormat = "#,##0"
$wsInfo.Cells.Item(22, 4).NumberFormat = "0.0%"
Add-DeviationColors $wsInfo.Range("D22")
$wsInfo.Cells.Item(23, 1) = "Sari kutudaki 3500 ornek bir gercek degerdir. Sapma hucresinin rengi bu farktan gelir. Temiz ve Flap sayfalarindaki sari kutular bos; yazinca ayni renk kurali calisir. Ek sayfasinda basinc orani ISA ile dolduruldu."

$wsInfo.Columns.Item(1).ColumnWidth = 28
$wsInfo.Columns.Item(2).ColumnWidth = 78
$wsInfo.Columns.Item(3).ColumnWidth = 16
$wsInfo.Columns.Item(4).ColumnWidth = 14
$wsInfo.Rows.Item(23).RowHeight = 48
$wsInfo.Rows.Item(23).WrapText = $true

$wb.Worksheets.Item(1).Activate()
$excel.Calculate()
$wb.SaveAs($path, 51)
$wb.Close($true)
$excel.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($wb) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
[GC]::Collect()
Write-Output "WROTE $path"
