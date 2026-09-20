# Build Level 1-39 route vs chart restriction workbook
$ErrorActionPreference = "Stop"
$root = "C:\Users\User\NavigationShare\Assets\_Game\Data Files"
$outXlsx = "C:\Users\User\NavigationShare\Level-1-39-Route-Chart-Restrictions.xlsx"
$outCsv = "C:\Users\User\NavigationShare\artifacts\Level-1-39-Route-Chart-Restrictions.csv"

function Get-YamlField {
    param([string]$Text, [string]$Key)
    $pattern = '(?m)^\s+' + [regex]::Escape($Key) + ':\s*([^\r\n]*)'
    $m = [regex]::Match($Text, $pattern)
    if (-not $m.Success) { return "" }
    $v = $m.Groups[1].Value.Trim().Trim("'").Trim('"')
    if ($v -match '^(Details|Name|RawSpeed|RawAltitude|RawDegrees|Distance|ID)\b') { return "" }
    return $v
}

function Format-DbAlt([string]$raw) {
    if ([string]::IsNullOrWhiteSpace($raw)) { return "(none)" }
    $v = $raw.Trim()
    if ($v -eq "140007000") { return "140007000 (concatenated FL140 + 7000)" }
    if ($v -match '^(\d+)A(\d+)B$') { return "BETWEEN $($Matches[1])-$($Matches[2])" }
    if ($v -match '^(\d+)C(\d+)$') { return "BETWEEN $($Matches[1])-$($Matches[2]) (C instead of A/B)" }
    if ($v -match '^(\d+)\s+(\d+)$') { return "BETWEEN $($Matches[1])-$($Matches[2]) (missing A/B)" }
    if ($v.EndsWith("A") -and $v -match '^(\d+)A$') { return "AT OR ABOVE $($Matches[1])" }
    if ($v.EndsWith("B") -and $v -match '^(\d+)B$') { return "AT OR BELOW $($Matches[1])" }
    if ($v -match '^\d+$') { return "AT $v" }
    return $v
}

function Format-DbSpd([string]$raw) {
    if ([string]::IsNullOrWhiteSpace($raw) -or $raw -eq "0") { return "(none)" }
    return "MAX $raw"
}

# Chart lookup: Level|Waypoint|Occ  or Level|Waypoint
# A = real altitude, S = real speed, N = note, R = source
$C = @{}
function AddC($k, $a, $s, $n, $r) { $C[$k] = @{ A = $a; S = $s; N = $n; R = $r } }

# ----- L1 EDDV ELNA2P / ILS 27R -----
AddC "1|_START" "(none)" "(none)" "Scenario start altitude, not a chart restriction" "Game"
AddC "1|ELNAT" "(none)" "(none)" "STAR fix; no published crossing restriction" "DFS ELNAT STAR family"
AddC "1|NORTA" "(none)" "(none)" "No NORTA-specific restriction. TMA MAX 250 kt below FL100 applies generally" "DFS / TMA note"
AddC "1|DLE-5" "(none)" "(none)" "Game-constructed DME fix; not a published STAR waypoint (intentionally used)" "n/a"
AddC "1|DV582" "(none)" "(none)" "Radar-vector box overlay; not on STAR plate" "EDDV radar overlay"
AddC "1|DV583" "(none)" "(none)" "Radar-vector box (C = pattern centre)" "EDDV radar overlay"
AddC "1|DV584" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|DV585" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|DV575" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|DV574" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|DV573" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|DV572" "(none)" "(none)" "Radar-vector box overlay" "EDDV radar overlay"
AddC "1|XAVER" "AT 3000" "(none)" "ILS-Z RWY 27R intercept 3000. 179 kt in DB is Vapp, not a plate MAX" "VATSIM Germany / ILS-Z 27R"
AddC "1|HANB" "AT 1400" "(none)" "3 deg GS at 4 NM ~1400 (THR 168). 173 kt in DB is Vapp" "EDDV SRA/ILS profile 4 NM"
AddC "1|RW27R" "THR 168" "(none)" "Threshold elevation; AD elev 183. Not a crossing restriction" "AIP AD elev / THR 27R"

# ----- L2 EDDV HLZ4R / ILS 09L -----
AddC "2|_START" "(none)" "(none)" "Scenario start altitude" "Game"
AddC "2|HLZ" "(none)" "(none)" "HLZ VOR; no published crossing on older HLZ STAR" "DFS"
AddC "2|CELNB" "AT OR BELOW 11000" "(none)" "CEL arrival max on older HLZ/CEL STAR" "Older EDDV STAR"
AddC "2|DV460" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV461" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV462" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV463" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV464" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV465" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV475" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV474" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV473" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DV472" "(none)" "(none)" "Radar-vector box" "EDDV radar overlay"
AddC "2|DESIM" "AT 3000" "(none)" "ILS-Z RWY 09L intercept 3000" "VATSIM Germany ILS-Z"
AddC "2|HWNB" "AT 1400" "(none)" "3 deg GS at ~4 NM" "EDDV ILS 09L profile"
AddC "2|RW09L" "THR 167" "(none)" "Threshold elevation; AD elev 183" "AIP"

# ----- L3 LFPG DINA4E / ILS 08L -----
AddC "3|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "3|DINAN" "(none)" "(none)" "STAR entry; no published crossing at DINAN" "SIA / DINA STAR"
AddC "3|CRL80" "AT 24000" "(none)" "Typical FL240 crossover; 280 kt is usual Mach/IAS, not always on plate" "LFPG DINA STAR"
AddC "3|ZERAM" "AT OR BELOW 18000" "(none)" "DINA STAR stepdown" "LFPG DINA STAR"
AddC "3|ANARU" "(none)" "(none)" "No published crossing" "LFPG DINA STAR"
AddC "3|DIVEM" "AT OR BELOW 14000" "(none)" "DINA STAR stepdown" "LFPG DINA STAR"
AddC "3|LORTA" "AT 14000" "(none)" "LORTA transition / IAF family" "LFPG LOR1H"
AddC "3|BUNOR" "AT OR ABOVE 11000" "(none)" "Initial approach min" "LFPG RNAV initial"
AddC "3|PG529" "AT OR ABOVE 6000" "(none)" "RNAV initial" "LFPG"
AddC "3|PG530" "(none)" "MAX 230" "Typical initial-approach speed" "LFPG initial"
AddC "3|PG532" "(none)" "(none)" "No published crossing" "LFPG"
AddC "3|CI08L" "AT 5000" "(none)" "Current ILS 08L default intercept 5000 (4000/3000/2000 available)" "IVAO LFPG memo AIRAC 2606"
AddC "3|OM08L" "AT 1840" "(none)" "3 deg OM / GS check" "LFPG ILS 08L"
AddC "3|RW08L" "THR 388" "(none)" "AD elev 392" "AIP LFPG"

# ----- L4 LFPG SABLO4W / ILS 26L -----
AddC "4|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "4|SABLE" "(none)" "(none)" "STAR entry" "LFPG SABLO STAR"
AddC "4|LUMAN" "AT 24000" "(none)" "FL240 STAR step" "LFPG SABLO"
AddC "4|ROMLO" "(none)" "(none)" "No published crossing" "LFPG SABLO"
AddC "4|BALOD" "AT 14000" "MAX 250" "BALOD crossing FL140 / 250 kt" "LFPG SABLO / BALOD trans"
AddC "4|CR43A" "AT 14000" "(none)" "Same FL140 platform" "LFPG"
AddC "4|DOMUS" "(none)" "MAX 220" "Initial approach speed" "LFPG"
AddC "4|PG517" "AT 13000" "(none)" "RNAV arrival step" "LFPG"
AddC "4|PG514" "AT OR ABOVE 12000" "(none)" "RNAV arrival min" "LFPG"
AddC "4|PG515" "(none)" "(none)" "No published crossing" "LFPG"
AddC "4|PG516" "(none)" "(none)" "No published crossing" "LFPG"
AddC "4|VECTOR" "(none)" "(none)" "Radar intercept, not a chart fix" "LFPG"
AddC "4|CI26L" "AT 4000" "(none)" "Current ILS 26L default 4000 (3000/2000 available)" "IVAO LFPG memo AIRAC 2606"
AddC "4|FI26L" "AT 2260" "(none)" "GS / FAF check altitude" "LFPG ILS 26L"
AddC "4|RW26L" "THR 366" "(none)" "AD elev 392" "AIP LFPG"

# ----- L5 EHAM REDF1A / ILS 06 -----
AddC "5|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "5|REDFA" "AT OR BELOW FL230" "(none)" "REDFA 1A MAX FL230. 280 kt not on STAR" "LVNL EHAM STAR AMDT 03/2026"
AddC "5|SULUT" "(none)" "(none)" "No published crossing" "LVNL STAR"
AddC "5|SUGOL" "BETWEEN FL070-FL100" "MAX 250" "IAF SUGOL FL070-FL100, MAX 250 KIAS" "LVNL STAR + IVAO NL briefing"
AddC "5|D2930" "(none)" "(none)" "Radial/DME leg, no published crossing" "LVNL"
AddC "5|SPL" "AT OR ABOVE 7000" "(none)" "SPL / IAF related min FL070" "LVNL"
AddC "5|CHNB|1" "AT OR ABOVE 3000" "(none)" "SUGOL 2A transition via CH" "EHAM IAC-06.2"
AddC "5|CHNB-4" "(none)" "(none)" "4 NM to CH, no extra published min" "EHAM IAC"
AddC "5|CHNB|2" "AT OR ABOVE 2000" "(none)" "Later CH crossing on ILS 06 transition" "EHAM IAC-06.2"
AddC "5|EH609" "AT 2000" "(none)" "FAF EH609 6.2 KAG 2000 AMSL" "opennav EHAM IAC-06-2"
AddC "5|EH616" "AT 1310" "MAX 160" "4.0 KAG 1310 AMSL, 160 KIAS" "opennav EHAM IAC-06-2"
AddC "5|RW06" "THR -11" "(none)" "THR 06 elev -11 ft" "AIP EHAM"

# ----- L6 EHAM PESE2A / ILS 18R -----
AddC "6|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "6|PESER" "AT OR BELOW FL070" "(none)" "PESER MAX FL070. 280 kt not on STAR" "LVNL STAR / IVAO NL"
AddC "6|STDNB" "(none)" "(none)" "Not on current PESER STAR (old NDB)" "LVNL current STAR"
AddC "6|RIVER" "BETWEEN FL070-FL100" "MAX 250" "IAF RIVER FL070-FL100, MAX 250" "LVNL STAR"
AddC "6|D2230" "(none)" "MAX 220" "220 KIAS at 15 DME SPL" "IVAO NL / EHAM AD 2.22"
AddC "6|SPL" "AT OR ABOVE 7000" "(none)" "SPL min FL070" "LVNL"
AddC "6|EH644" "AT OR ABOVE 3000" "(none)" "18R transition min" "EHAM IAC 18R"
AddC "6|INTC" "(none)" "(none)" "Radar intercept" "EHAM"
AddC "6|EH621" "AT 2000" "(none)" "FAF / platform 2000" "EHAM IAC 18R"
AddC "6|EH622" "AT 1310" "MAX 160" "Same 4 NM GS check as 06 family" "EHAM IAC"
AddC "6|RW18R" "THR -11" "(none)" "Schiphol THR near -11 ft" "AIP EHAM"

# ----- L7 EGLL BNN1B / ILS 09L -----
AddC "7|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "7|NUGRA" "(none)" "MAX 250" "SLP: 250 KIAS 3 min before hold. No NUGRA altitude" "UK AIP BNN 1B 2014"
AddC "7|TOBID" "AT OR BELOW FL200" "(none)" "BNN 1B: FL200 by TOBID" "UK AIP EGLL 7-3 / NUGRA 1H"
AddC "7|SOPIT" "AT OR BELOW FL150" "MAX 220" "FL150 by SOPIT; LTMA hold MAX 220 through FL140" "UK AIP BNN 1B / NUGRA 1H"
AddC "7|WCONB" "(none)" "(none)" "WCO NDB; no extra crossing" "UK AIP"
AddC "7|BNN" "AT 7000" "MAX 220" "BNN stack lowest 7000; hold MAX 220 to FL140" "UK AIP BNN STAR"
AddC "7|BNN05" "(none)" "(none)" "Initial approach / radar" "UK AIP IAP notes"
AddC "7|LAM30" "(none)" "(none)" "Radar vector leg" "UK AIP"
AddC "7|LAM34" "(none)" "(none)" "Radar vector leg" "UK AIP"
AddC "7|GWC39" "(none)" "(none)" "Radar vector leg" "UK AIP"
AddC "7|IAA10" "(none)" "(none)" "I-AA DME 10, not a published restriction" "UK AIP ILS 09L"
AddC "7|CF09L" "AT 2500" "(none)" "ILS/DME I-AA intercept 2500" "UK AIP EGLL 8-1"
AddC "7|FF09L" "AT 1400" "(none)" "GP/FAP 1400 (I-AA D4)" "UK AIP EGLL 8-1"
AddC "7|RW09L" "THR 79" "(none)" "AD elev 83" "UK AIP"

# ----- L8 EGLL OCK1A / ILS 27L -----
AddC "8|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "8|KENET" "AT OR BELOW FL140" "(none)" "OCK 1A: FL140 by 40 NM before OCK (KENET is OCK D40). 7000 is OCK stack, not KENET" "UK AIP EGLL 7-6 (2013)"
AddC "8|D291L" "(none)" "MAX 250" "SLP / 3 min before hold MAX 250 KIAS" "UK AIP OCK STAR"
AddC "8|OCK" "AT 7000" "MAX 220" "OCK stack min 7000; hold MAX 220 through FL140" "UK AIP OCK STAR"
AddC "8|OCK07" "AT OR ABOVE 7000" "MAX 220" "Leaving stack / 7 DME OCK" "UK AIP / initial"
AddC "8|OC11A" "AT OR ABOVE 6000" "(none)" "Intermediate 6000" "Heathrow initial"
AddC "8|OC12B" "(none)" "(none)" "No published crossing" "Heathrow"
AddC "8|INTC" "(none)" "(none)" "Radar intercept" "UK AIP"
AddC "8|ILL10" "AT 3000" "(none)" "I-LL D10 / 3000 platform" "UK AIP ILS 27L"
AddC "8|CF27L" "AT 2500" "(none)" "ILS intercept 2500" "UK AIP EGLL 8-9"
AddC "8|FF27L" "AT 1400" "(none)" "GP/FAP 1400" "UK AIP EGLL 8-9"
AddC "8|RW27L" "THR 77" "(none)" "AD elev 83" "UK AIP"

# ----- L9 EKCH ALM3M / ILS 04L -----
AddC "9|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "9|ALM" "(none)" "(none)" "ALM VOR STAR entry" "EKCH ALM STAR"
AddC "9|REPRO" "(none)" "MAX 250" "TMA 250; not always on the fix" "EKCH"
AddC "9|CH552" "AT OR ABOVE 5000" "MAX 220" "RNAV downwind 5000+ / 220" "EKCH RNAV STAR 04L"
AddC "9|CH551" "AT OR ABOVE 4000" "MAX 220" "RNAV downwind 4000+ / 220" "EKCH RNAV STAR 04L"
AddC "9|CH441" "(none)" "(none)" "Base / intercept turn" "EKCH"
AddC "9|CI04L" "AT 3000" "(none)" "ILS 04L intercept 3000" "EKCH ILS 04L"
AddC "9|FI04L" "AT 1600" "(none)" "FAF / GS ~1600" "EKCH ILS 04L"
AddC "9|RW04L" "THR 17" "(none)" "AD elev 17" "AIP EKCH"

# ----- L10 EKCH CDA3N / ILS 22L -----
AddC "10|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "10|CDA" "(none)" "(none)" "CDA VOR STAR entry" "EKCH CDA STAR"
AddC "10|KUTAX" "(none)" "(none)" "No published crossing" "EKCH"
AddC "10|CH993" "(none)" "(none)" "RNAV track, no published min" "EKCH"
AddC "10|CH992" "(none)" "(none)" "RNAV track, no published min" "EKCH"
AddC "10|CH991" "AT OR ABOVE 5000" "(none)" "RNAV downwind 5000+" "EKCH RNAV 22L"
AddC "10|CH881" "(none)" "(none)" "Base turn" "EKCH"
AddC "10|LAMOX" "AT OR ABOVE 3000" "(none)" "IF 3000+" "EKCH ILS 22L"
AddC "10|93DME" "AT 3000" "(none)" "LOC intercept 3000" "EKCH ILS 22L"
AddC "10|FI22L" "AT 1600" "(none)" "FAF / GS ~1600" "EKCH ILS 22L"
AddC "10|RW22L" "THR 12" "(none)" "AD elev 17" "AIP EKCH"

# ----- L11 LIRF LAT3A / ILS 16C -----
AddC "11|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "11|LAT" "(none)" "(none)" "LAT VOR STAR entry" "LIRF LAT STAR"
AddC "11|ROM" "(none)" "(none)" "ROM VOR; no published crossing on older LAT3A" "LIRF"
AddC "11|INTC" "(none)" "(none)" "Radar intercept" "LIRF"
AddC "11|MIKSO" "AT OR ABOVE 2500" "(none)" "IAF 2500+" "LIRF ILS 16C"
AddC "11|CI16C" "AT 2500" "(none)" "LOC intercept 2500" "LIRF ILS 16C"
AddC "11|FI16C" "AT 1340" "(none)" "FAF / GS" "LIRF ILS 16C"
AddC "11|RW16C" "THR 13" "(none)" "AD elev 13" "AIP LIRF"

# ----- L12 LIRF ELKA3A / ILS 25 -----
AddC "12|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "12|ELKAP" "(none)" "(none)" "STAR entry" "LIRF ELKA STAR"
AddC "12|BIBEK" "(none)" "(none)" "No published crossing on older ELKA3A" "LIRF"
AddC "12|TAQ" "(none)" "(none)" "TAQ VOR; altitude usually ATC" "LIRF"
AddC "12|CMP" "(none)" "(none)" "No published crossing" "LIRF"
AddC "12|URBNB" "(none)" "(none)" "Urban NDB area" "LIRF"
AddC "12|INTC" "(none)" "(none)" "Radar intercept" "LIRF"
AddC "12|CF25" "AT 3000" "(none)" "ILS 25 intercept 3000" "LIRF ILS 25"
AddC "12|FF25" "AT 3000" "(none)" "Platform / FAF 3000 on this ILS" "LIRF ILS 25"
AddC "12|RW25" "THR 13" "(none)" "AD elev 13" "AIP LIRF"

# ----- L13 LSZH NEGR1A / ILS 14 -----
AddC "13|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "13|NEGRA" "(none)" "(none)" "STAR entry" "LSZH NEGR STAR"
AddC "13|MATIV" "(none)" "(none)" "No published crossing" "LSZH"
AddC "13|AMIKI" "(none)" "(none)" "AMIKI IAF / hold; mins on later fixes" "LSZH STAR AMIKI"
AddC "13|ZUE" "AT OR ABOVE 7000" "(none)" "ZUE 7000+" "LSZH ILS 14 / AMIKI STAR"
AddC "13|ZH701" "AT OR ABOVE 6000" "(none)" "RNAV 6000+" "LSZH"
AddC "13|TRA" "AT OR ABOVE 5000" "(none)" "TRA 5000+" "LSZH"
AddC "13|INTC" "(none)" "(none)" "Radar intercept" "LSZH"
AddC "13|CF14" "AT 4000" "(none)" "ILS 14 intercept 4000" "LSZH ILS 14"
AddC "13|OSNEM" "AT 4000" "(none)" "GS / FAF 4000" "LSZH ILS 14"
AddC "13|RW14" "THR 1390" "(none)" "AD elev 1416" "LSZH AIP"

# ----- L14 LSZH BLM2G / ILS 34 -----
AddC "14|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "14|BLM" "AT OR BELOW FL200" "(none)" "BLM arrival is FL200-class, not 2000 ft (field 1416)" "LSZH BLM STAR"
AddC "14|ZH677" "AT OR ABOVE 12000" "(none)" "RNAV 12000+" "LSZH BLM RNAV"
AddC "14|GIPOL" "AT OR ABOVE 7000" "(none)" "GIPOL 7000" "LSZH STAR GIPOL / ILS 34"
AddC "14|KLO" "AT OR ABOVE 6000" "(none)" "KLO 6000" "LSZH ILS 34"
AddC "14|ZH726" "AT 6000" "(none)" "Intermediate 6000" "LSZH"
AddC "14|UTIXO" "AT 5000" "(none)" "Intermediate 5000" "LSZH"
AddC "14|MILNI" "AT 2710" "(none)" "ILS 34 GS D3.8 ~2710, not 1458" "Jepp ILS DME 34"
AddC "14|RW24" "THR 1388 (RWY 34)" "(none)" "Zurich has no RW24. ILS 34 ident IZS 110.75. THR 34 = 1388" "LSZH AIP / Level Info RW 34"

# ----- L15 LEBL VERDB / ILS 02 -----
AddC "15|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "15|VERSO" "(none)" "(none)" "STAR entry" "LEBL VER STAR"
AddC "15|BL038" "(none)" "(none)" "No published crossing" "LEBL"
AddC "15|BL012" "(none)" "(none)" "No published crossing" "LEBL"
AddC "15|VIBIM" "BETWEEN 4000-7000" "(none)" "VIBIM window 4000-7000" "LEBL VIBIM trans"
AddC "15|INTC" "(none)" "(none)" "Radar intercept" "LEBL"
AddC "15|SANIS" "AT 2000" "(none)" "ILS 02 intercept 2000" "LEBL ILS 02"
AddC "15|DM06A" "AT 2000" "(none)" "GS / FAF 2000" "LEBL ILS 02"
AddC "15|RW02" "THR 12" "(none)" "AD elev 12" "AIP LEBL"

# ----- L16 LEBL BIS2DD / ILS 25R -----
AddC "16|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "16|BISBA" "(none)" "(none)" "STAR entry" "LEBL BIS STAR"
AddC "16|BGR" "(none)" "(none)" "BGR VOR" "LEBL"
AddC "16|BL045" "(none)" "(none)" "No published crossing" "LEBL"
AddC "16|XAMUR" "(none)" "(none)" "No published crossing" "LEBL"
AddC "16|D0970" "(none)" "(none)" "Radial/DME" "LEBL"
AddC "16|LESBA" "AT OR ABOVE 4000" "(none)" "LESBA 4000+" "LEBL LESBA trans"
AddC "16|SLL-16" "(none)" "(none)" "SLL 16 DME" "LEBL"
AddC "16|TEBLA" "AT OR ABOVE 2300" "(none)" "TEBLA 2300+" "LEBL ILS 25R"
AddC "16|CI25R" "AT 2300" "(none)" "ILS 25R intercept 2300" "LEBL ILS 25R"
AddC "16|FU25R" "AT 1351" "(none)" "GS / FAF" "LEBL ILS 25R"
AddC "16|RW25R" "THR 12" "(none)" "AD elev 12" "AIP LEBL"

# ----- L17 EBBR ARVO2A / ILS 02 -----
AddC "17|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "17|ARVOL" "(none)" "MAX 250" "STAR speed 250; no ARVOL altitude" "EBBR ARVO STAR"
AddC "17|AKOVI" "(none)" "(none)" "No published crossing" "EBBR"
AddC "17|RODRI" "(none)" "(none)" "No published crossing" "EBBR"
AddC "17|KERKY" "(none)" "(none)" "KERKY trans; altitude ATC" "EBBR"
AddC "17|AFI" "(none)" "(none)" "AFI VOR" "EBBR"
AddC "17|AFI-12" "(none)" "(none)" "AFI 12 DME" "EBBR"
AddC "17|NIVOR" "AT OR ABOVE 2000" "(none)" "NIVOR 2000+" "EBBR ILS 02"
AddC "17|HUL-11" "(none)" "(none)" "HUL 11 DME" "EBBR"
AddC "17|CF02" "AT 2000" "(none)" "ILS 02 intercept 2000" "EBBR ILS 02"
AddC "17|OM02" "AT 1430" "(none)" "OM / GS" "EBBR ILS 02"
AddC "17|RW02" "THR 184" "(none)" "AD elev 184" "AIP EBBR"

# ----- L18 EBBR TULN3B / ILS 25R -----
AddC "18|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "18|TULNI" "(none)" "MAX 250" "STAR speed 250" "EBBR TULN STAR"
AddC "18|CIV" "(none)" "(none)" "CIV VOR" "EBBR"
AddC "18|HUL" "(none)" "(none)" "HUL NDB" "EBBR"
AddC "18|FLO" "(none)" "(none)" "FLO trans" "EBBR"
AddC "18|FLO-10" "AT OR ABOVE 2000" "(none)" "FLO 10 DME 2000+" "EBBR ILS 25R"
AddC "18|INTC" "(none)" "(none)" "Radar intercept" "EBBR"
AddC "18|CF25R" "AT 2000" "(none)" "ILS 25R intercept 2000" "EBBR ILS 25R"
AddC "18|OM25R" "AT 1400" "(none)" "OM / GS" "EBBR ILS 25R"
AddC "18|RW25R" "THR 160" "(none)" "THR 25R ~160; AD elev 184" "AIP EBBR"

# ----- L19 LOWW GIGOW1W / ILS 11 -----
AddC "19|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "19|GIGOR" "AT OR BELOW FL160" "MAX 250" "GIGOR 1W: at or below FL160; SLP 250" "Jepp GIGOR 1W / Austro Control"
AddC "19|PESAT" "AT OR ABOVE 6000" "(none)" "PESAT A6000+" "Austro Control LOWW STAR coding"
AddC "19|WW841" "AT OR ABOVE 6000" "(none)" "Older PESAT 11 transition 6000+" "vACC Austria trans 11 (older WW numbers)"
AddC "19|WW843" "AT OR ABOVE 6000" "(none)" "Older trans 6000+" "vACC Austria"
AddC "19|WW844" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW845" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW846" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW813" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW812" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW811" "AT OR ABOVE 5000" "(none)" "Older trans 5000+" "vACC Austria"
AddC "19|WW919" "AT OR ABOVE 3400" "(none)" "IF 3400+" "LOWW ILS 11"
AddC "19|CI11X" "AT 3400" "(none)" "ILS 11 intercept 3400" "LOWW ILS 11"
AddC "19|36DME" "AT 1765" "(none)" "GS / FAF" "LOWW ILS 11"
AddC "19|RW11" "THR 600" "(none)" "AD elev 600" "AIP LOWW"

# ----- L20 LOWW LANU5W / ILS 34 -----
AddC "20|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "20|LANUX" "AT OR BELOW FL150" "(none)" "LANUX F150-" "Austro Control LOWW STAR coding 2026"
AddC "20|NERDU" "AT OR ABOVE 6000" "(none)" "NERDU A6000+" "Austro Control LOWW STAR"
AddC "20|WW925" "AT OR ABOVE 6000" "(none)" "NER trans 6000+" "LOWW RNAV trans 34"
AddC "20|WW926" "AT OR ABOVE 6000" "(none)" "NER trans 6000+" "LOWW"
AddC "20|WW927" "AT OR ABOVE 4000" "(none)" "NER trans 4000+" "LOWW"
AddC "20|WW924" "AT OR ABOVE 4000" "(none)" "NER trans 4000+" "LOWW"
AddC "20|WW834" "AT OR ABOVE 3000" "(none)" "IF 3000+" "LOWW ILS 34"
AddC "20|CI34" "AT 3000" "(none)" "ILS 34 intercept 3000" "LOWW ILS 34"
AddC "20|OM34" "AT 2004" "(none)" "OM / GS" "LOWW ILS 34"
AddC "20|RW34" "THR 600" "(none)" "AD elev 600" "AIP LOWW"

# ----- L21 LTFJ GELG1G / ILS 06 -----
AddC "21|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "21|GELBU" "(none)" "(none)" "STAR entry; no published min in game AIRAC name" "LTFJ GELG STAR"
AddC "21|TURKO" "(none)" "(none)" "No published crossing" "LTFJ"
AddC "21|FJ510" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ511" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ512" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ513" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|BUKED" "(none)" "(none)" "No published crossing" "LTFJ"
AddC "21|FJ515" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ516" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ517" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ518" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|FJ519" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "21|DISC" "(none)" "(none)" "Discontinuity / radar" "LTFJ"
AddC "21|ASDEV" "(none)" "(none)" "ASDEV trans" "LTFJ"
AddC "21|CI06" "AT 3000" "(none)" "Current ILS 06R: intercept LLZ at IF, not below 3000" "DHMI LTFJ IAC 15"
AddC "21|FI06" "AT 3000" "(none)" "GP intercept at FAP ~10.5 DME on 3000 platform" "DHMI LTFJ IAC 15"
AddC "21|RW06" "THR 270" "(none)" "THR 06R 270; AD elev 312" "DHMI / AIP LTFJ"

# ----- L22 LTFJ TETS1H / ILS 24 -----
AddC "22|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "22|TETSA" "(none)" "(none)" "STAR entry" "LTFJ TETS STAR"
AddC "22|PABDO" "AT 14000" "MAX 250" "TETS arrival step FL140 / 250" "LTFJ TETS1H"
AddC "22|PAZAR" "(none)" "(none)" "No published crossing" "LTFJ"
AddC "22|MEBOV" "AT 9000" "MAX 220" "TETS arrival 9000 / 220" "LTFJ TETS1H"
AddC "22|FJ931" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "22|FJ932" "AT 5000" "(none)" "Downwind 5000" "LTFJ"
AddC "22|FJ933" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "22|FJ934" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "22|FJ935" "(none)" "(none)" "RNAV track" "LTFJ"
AddC "22|DISC" "(none)" "(none)" "Discontinuity / radar" "LTFJ"
AddC "22|BEMKA" "AT OR ABOVE 3500" "(none)" "BEMKA 3500+" "LTFJ ILS 24"
AddC "22|CI24" "AT 3500" "(none)" "ILS 24 intercept 3500" "LTFJ ILS 24 / IAC MSA 3500"
AddC "22|FI24" "AT 1800" "(none)" "3 deg FAF ~1800 (THR ~290). Some IAC versions show higher FAF" "LTFJ ILS 24 profile"
AddC "22|RW24" "THR 289" "(none)" "AD elev 312" "AIP LTFJ"

# ----- L23 VTBS CABI1B / ILS 01L -----
AddC "23|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "23|CABIN" "AT OR ABOVE 9000" "MAX 240" "CABIN 1B: 9000+ MAX 240" "Jepp CABIN 1B STAR"
AddC "23|BS652" "AT OR ABOVE 7000" "MAX 210" "Next fix 7000+ MAX 210" "Jepp CABIN 1B"
AddC "23|BS651" "AT OR ABOVE 6000" "(none)" "6000+" "Jepp CABIN 1B"
AddC "23|BYLON" "AT OR ABOVE 4000" "(none)" "4000+" "Jepp CABIN 1B"
AddC "23|SHEEN" "AT OR ABOVE 2000" "(none)" "2000+" "Jepp CABIN 1B"
AddC "23|LASER" "AT OR ABOVE 2000" "MIN 170" "2000+; MIN 170 on some legs" "Jepp CABIN 1B"
AddC "23|LEVIN" "AT 2000" "(none)" "IF / 2000" "VTBS ILS 01"
AddC "23|FI09L" "AT 1290 (FI01L)" "(none)" "GS FAF ~1290. Chart name is FI01L not FI09L" "VTBS ILS 01L"
AddC "23|RW01L" "THR 5" "(none)" "AD elev 5" "AIP VTBS"

# ----- L24 VTBS SILV1B / ILS 19L -----
AddC "24|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "24|SILVA" "AT OR ABOVE 9000" "MAX 240" "SILVA 1B: 9000+ MAX 240" "Jepp SILVA 1B STAR"
AddC "24|BAKKE" "AT OR ABOVE 7000" "MAX 210" "BAKKE 7000+ MAX 210 (240 belongs to SILVA)" "Jepp SILVA 1B"
AddC "24|PETER" "AT OR ABOVE 4000" "MAX 210" "PETER 4000+ / 210" "Jepp SILVA 1B"
AddC "24|ROBIN" "AT OR ABOVE 2000" "(none)" "2000+" "Jepp SILVA 1B"
AddC "24|LYNDA" "AT OR ABOVE 2000" "(none)" "2000+" "Jepp SILVA 1B"
AddC "24|LEMON" "AT 2000" "(none)" "IF 2000" "VTBS ILS 19L"
AddC "24|FI19L" "AT 1300" "(none)" "GS FAF ~1300" "VTBS ILS 19L"
AddC "24|RW19L" "THR 5" "(none)" "AD elev 5" "AIP VTBS"

# ----- L25 WADD GAGA1B / ILS 27 -----
AddC "25|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "25|GAGAK" "(none)" "(none)" "STAR entry" "WADD GAGA STAR"
AddC "25|KEDAT" "AT 7000" "(none)" "KEDAT 7000" "WADD GAGA1B"
AddC "25|BENOA" "AT OR ABOVE 3000" "(none)" "BENOA 3000+" "WADD BENOA trans"
AddC "25|97DME" "AT 2000" "(none)" "ILS intercept 2000" "WADD ILS 27"
AddC "25|62DME" "AT 2000" "(none)" "GS / FAF 2000" "WADD ILS 27"
AddC "25|RW27" "THR 14" "(none)" "AD elev 14" "AIP WADD"

# ----- L26 WIII CARLI2 / ILS 07R -----
AddC "26|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "26|CARLI" "(none)" "(none)" "STAR entry" "WIII CARLI STAR"
AddC "26|DKI44" "(none)" "(none)" "DKI 44 DME" "WIII"
AddC "26|NOKTA" "(none)" "(none)" "NOKTA trans; altitude ATC" "WIII"
AddC "26|DKI28" "AT OR ABOVE 6000" "(none)" "DKI 28 DME 6000+" "WIII CARLI/NOKTA"
AddC "26|DKI-42" "(none)" "(none)" "DKI radial" "WIII"
AddC "26|INTC" "(none)" "(none)" "Radar intercept" "WIII"
AddC "26|CF07R" "AT 3000" "(none)" "ILS 07R intercept 3000" "WIII ILS 07R"
AddC "26|OM07R" "AT 1693" "(none)" "OM / GS" "WIII ILS 07R"
AddC "26|RW07R" "THR 34" "(none)" "AD elev 34" "AIP WIII"

# ----- L27 VVTS T10 / ILS 25L -----
AddC "27|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "27|POPET" "(none)" "(none)" "STAR entry" "VVTS T10"
AddC "27|SAPEN" "(none)" "(none)" "No published crossing" "VVTS"
AddC "27|TSN" "(none)" "(none)" "TSN VOR" "VVTS"
AddC "27|DISC" "(none)" "(none)" "Discontinuity / radar" "VVTS"
AddC "27|CI25L" "AT 1750" "(none)" "ILS 25L intercept 1750" "VVTS ILS 25L"
AddC "27|SGNB" "AT 1750" "(none)" "Same intercept platform" "VVTS ILS 25L"
AddC "27|RW25L" "THR 33" "(none)" "AD elev 33" "AIP VVTS"

# ----- L28 FACT WY4C / ILS 01 -----
AddC "28|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "28|WYNB" "(none)" "(none)" "WY NDB STAR entry" "FACT WY STAR"
AddC "28|CTV30" "(none)" "MAX 250" "30 DME / 250 kt" "FACT WY4C"
AddC "28|D065H" "(none)" "MAX 210" "Arc speed 210" "FACT WY4C"
AddC "28|VECTOR" "(none)" "(none)" "Radar intercept" "FACT"
AddC "28|CI01" "AT 2000" "(none)" "ILS 01 intercept 2000" "FACT ILS 01"
AddC "28|FI01" "AT 1320" "(none)" "GS / FAF" "FACT ILS 01"
AddC "28|RW01" "THR 151" "(none)" "AD elev 151" "AIP FACT"

# ----- L29 HKJK AVIT1P / ILS 06 -----
AddC "29|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "29|AVITU" "(none)" "(none)" "AVITU STAR entry; then radar or TV hold" "HKJK AVITU STAR"
AddC "29|TV" "AT OR ABOVE FL100" "(none)" "Leave TH/TV at FL100" "Jepp HKJK AVITU 2P"
AddC "29|TV-11" "(none)" "(none)" "TV 11 DME outbound" "HKJK ILS 06 via TV"
AddC "29|CI06" "AT 7500" "(none)" "Descend in hold, leave FL100, descend to 7500 then ILS 06. Field is 5330 - 3200 AMSL is below the airport" "Jepp HKJK ILS 06 via TV"
AddC "29|NONB" "AT 6500" "(none)" "3 deg FAF ~4 NM: ~6650 AMSL. Game 1000 is below field" "HKJK ILS 06 GS"
AddC "29|RW06" "THR 5330" "(none)" "AD elev 5327-5330" "AIP HKJK"

# ----- L30 GMMX BGA7A / ILS 10 -----
AddC "30|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "30|OBOGA" "(none)" "(none)" "STAR / BGA family entry" "GMMX BGA STAR"
AddC "30|D032M" "(none)" "(none)" "DME arc" "GMMX"
AddC "30|D013M" "(none)" "(none)" "DME arc" "GMMX"
AddC "30|D354M" "(none)" "MAX 230" "Arc speed 230" "GMMX ILS 10"
AddC "30|D332M" "(none)" "(none)" "DME arc" "GMMX"
AddC "30|D310M" "(none)" "(none)" "DME arc" "GMMX"
AddC "30|D288M" "(none)" "(none)" "DME arc" "GMMX"
AddC "30|CI10" "AT 3200" "(none)" "ILS 10 intercept 3200" "GMMX ILS 10"
AddC "30|MAK" "AT 2390" "(none)" "GS / FAF via MAK" "GMMX ILS 10"
AddC "30|RW10" "THR 1545" "(none)" "AD elev 1545" "AIP GMMX"

# ----- L31 DTTA NOLSI / ILS 29 -----
AddC "31|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "31|NOLSI" "(none)" "(none)" "STAR entry" "DTTA NOLSI"
AddC "31|MARSA" "AT OR ABOVE 3000" "(none)" "MARSA 3000+" "DTTA MARSA trans"
AddC "31|D062M" "(none)" "(none)" "DME arc" "DTTA"
AddC "31|D082M" "(none)" "(none)" "DME arc" "DTTA"
AddC "31|D103M" "AT OR ABOVE 2200" "(none)" "Arc 2200+" "DTTA ILS 29"
AddC "31|CI29" "AT 2200" "(none)" "ILS 29 intercept 2200" "DTTA ILS 29"
AddC "31|FI29" "AT 2200" "(none)" "GS platform 2200" "DTTA ILS 29"
AddC "31|RW29" "THR 22" "(none)" "AD elev 22" "AIP DTTA"

# ----- L32 RJTT NYLON2 / ILS 22 -----
AddC "32|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "32|ADDUM" "AT 10000" "(none)" "ADDUM 10000" "RJTT NYLON arrival"
AddC "32|BLITZ" "(none)" "(none)" "RNAV track" "RJTT"
AddC "32|BRASS" "(none)" "(none)" "RNAV track" "RJTT"
AddC "32|NYLON" "(none)" "(none)" "NYLON; mins at NEXUS/NITRO" "RJTT NYLON2"
AddC "32|NIFTY" "(none)" "(none)" "RNAV track" "RJTT"
AddC "32|NEXUS" "AT 5000" "(none)" "NEXUS 5000" "RJTT NYLON / ILS 22"
AddC "32|NITRO" "AT 5000" "(none)" "NITRO 5000" "RJTT ILS 22"
AddC "32|RW22" "THR 21" "(none)" "AD elev 21" "AIP RJTT"

# ----- L33 RJTT CREAM / ILS 34L -----
AddC "33|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "33|STONE" "AT 11000" "MAX 250" "STONE FL110 / 250" "RJTT CREAM arrival"
AddC "33|TLE" "(none)" "(none)" "TLE VOR" "RJTT"
AddC "33|CURRY" "(none)" "(none)" "RNAV track" "RJTT"
AddC "33|COUPE" "AT OR ABOVE 8000" "(none)" "COUPE 8000+" "RJTT CREAM"
AddC "33|CUTIE" "(none)" "(none)" "RNAV track" "RJTT"
AddC "33|CREAM" "AT OR ABOVE 5000" "(none)" "CREAM 5000+" "RJTT CREAM"
AddC "33|ARLON" "AT 5000" "(none)" "IF 5000" "RJTT ILS 34L"
AddC "33|APOLO" "AT 5000" "(none)" "FAF 5000" "RJTT ILS 34L"
AddC "33|RW34L" "THR 21" "(none)" "AD elev 21" "AIP RJTT"

# ----- L34 VIDP AKBA2B / ILS 11 -----
AddC "34|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "34|AKBAN" "AT OR ABOVE 12000" "(none)" "AKBAN 12000+" "VIDP AKBA STAR"
AddC "34|CHI" "(none)" "(none)" "CHI VOR trans" "VIDP"
AddC "34|D240T" "AT OR ABOVE 6500" "(none)" "R240 / 20 DME 6500+" "VIDP ILS 11"
AddC "34|D240O" "AT OR ABOVE 2600" "(none)" "R240 / 15 DME 2600+" "VIDP ILS 11"
AddC "34|D253M" "(none)" "(none)" "Arc" "VIDP"
AddC "34|D267M" "AT OR ABOVE 2600" "(none)" "Arc 2600+" "VIDP ILS 11"
AddC "34|CI11" "AT 2600" "(none)" "ILS 11 intercept 2600" "VIDP ILS 11"
AddC "34|FI11" "AT 2000" "(none)" "GS / FAF 2000" "VIDP ILS 11"
AddC "34|RW11" "THR 777" "(none)" "AD elev 777" "AIP VIDP"

# ----- L35 VIDP BASO1A / ILS 28 -----
AddC "35|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "35|BASOT" "(none)" "(none)" "STAR entry" "VIDP BASO STAR"
AddC "35|CHI27" "(none)" "(none)" "CHI 27 DME" "VIDP"
AddC "35|DI26Z" "AT OR ABOVE 8000" "(none)" "8000+" "VIDP BASO1A"
AddC "35|RUGDA" "(none)" "(none)" "No published crossing" "VIDP"
AddC "35|D274V" "AT OR ABOVE 6500" "(none)" "6500+" "VIDP ILS 28"
AddC "35|PEDKA" "(none)" "(none)" "No published crossing" "VIDP"
AddC "35|DI170" "(none)" "(none)" "Radial/DME" "VIDP"
AddC "35|VENGI" "(none)" "(none)" "No published crossing" "VIDP"
AddC "35|CI28" "AT 2600" "(none)" "ILS 28 intercept 2600" "VIDP ILS 28"
AddC "35|OM28" "AT 2140" "(none)" "OM / GS" "VIDP ILS 28"
AddC "35|RW288" "THR 777 (RWY 28)" "(none)" "Waypoint name typo; runway is 28. AD elev 777" "AIP VIDP"

# ----- L36 RPLL CAB1 / ILS 06 -----
AddC "36|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "36|CAB" "(none)" "(none)" "CAB VOR STAR entry" "RPLL CAB STAR"
AddC "36|TARTA" "(none)" "(none)" "No published crossing" "RPLL"
AddC "36|GARAY" "AT OR ABOVE 7000" "(none)" "GARAY 7000+" "RPLL CAB1"
AddC "36|MIA" "AT OR ABOVE 4000" "(none)" "MIA 4000+" "RPLL MIA trans"
AddC "36|MIA-10" "(none)" "(none)" "MIA 10 DME" "RPLL"
AddC "36|CI06" "AT 2500" "(none)" "ILS 06 intercept 2500" "RPLL ILS 06"
AddC "36|FI06" "AT 2500" "(none)" "GS platform 2500" "RPLL ILS 06"
AddC "36|RW06" "THR 75" "(none)" "AD elev 75" "AIP RPLL"

# ----- L37 RPLL IPA1 / ILS 24 -----
AddC "37|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "37|IPATA" "(none)" "(none)" "STAR entry" "RPLL IPA STAR"
AddC "37|KABAN" "AT OR ABOVE 10000" "(none)" "KABAN 10000+" "RPLL IPA1"
AddC "37|CAMBA" "AT OR ABOVE 6000" "(none)" "CAMBA 6000+" "RPLL IPA1"
AddC "37|MIA" "AT OR ABOVE 4000" "(none)" "MIA 4000+" "RPLL MIA trans"
AddC "37|MIA-9" "(none)" "(none)" "MIA 9 DME" "RPLL"
AddC "37|CF24" "AT 2500" "(none)" "ILS 24 intercept 2500" "RPLL ILS 24"
AddC "37|FF24" "AT 1424" "(none)" "GS / FAF" "RPLL ILS 24"
AddC "37|RW24" "THR 75" "(none)" "AD elev 75" "AIP RPLL"

# ----- L38 UUWW DR19A / ILS 19 -----
AddC "38|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "38|DRNB" "(none)" "(none)" "DR NDB STAR entry" "UUWW DR STAR"
AddC "38|GOTMA" "(none)" "(none)" "No published crossing" "UUWW"
AddC "38|054KS" "AT OR ABOVE 4900" "(none)" "KS 054 / 4900+" "UUWW DR19A"
AddC "38|KSNB" "AT 3590" "(none)" "KS NDB 3590" "UUWW"
AddC "38|180SX" "(none)" "(none)" "Radial" "UUWW"
AddC "38|D1690" "AT 1950" "(none)" "FAF / GS 1950" "UUWW ILS 19"
AddC "38|89ITA" "AT 1950" "(none)" "Same GS platform" "UUWW ILS 19"
AddC "38|RW19" "THR 686" "(none)" "AD elev 686. 1946 in DB is the FAF altitude, not threshold" "AIP UUWW"

# ----- L39 UUWW GIS24R / ILS 24 -----
AddC "39|_START" "(none)" "(none)" "Scenario start" "Game"
AddC "39|GISIN" "(none)" "(none)" "STAR entry" "UUWW GIS STAR"
AddC "39|OKLIT" "(none)" "(none)" "No published crossing" "UUWW"
AddC "39|TURUG" "(none)" "(none)" "No published crossing" "UUWW"
AddC "39|WW521" "AT OR ABOVE 5900" "MAX 230" "5900+ / 230" "UUWW GIS24R"
AddC "39|WW522" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW523" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW524" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW525" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW505" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW504" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|DRNB" "AT 5900" "(none)" "DR 5900" "UUWW GIS24R"
AddC "39|WW502" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|WW501" "(none)" "(none)" "RNAV track" "UUWW"
AddC "39|OSTIS" "AT 3640" "(none)" "OSTIS 3640" "UUWW OSTIS trans"
AddC "39|290OB" "AT 2650" "(none)" "OB 290 / 2650" "UUWW ILS 24"
AddC "39|258OB" "AT 2320" "(none)" "OB 258 / 2320" "UUWW ILS 24"
AddC "39|CF24" "AT 2000" "(none)" "ILS 24 intercept 2000" "UUWW ILS 24"
AddC "39|FF24" "AT 1991" "(none)" "GS / FAF" "UUWW ILS 24"
AddC "39|RW24" "THR 686" "(none)" "AD elev 686" "AIP UUWW"

function Get-ChartRow($level, $wpt, $occ) {
    $k1 = "$level|$wpt|$occ"
    $k2 = "$level|$wpt"
    if ($C.ContainsKey($k1)) { return $C[$k1] }
    if ($C.ContainsKey($k2)) { return $C[$k2] }
    return @{ A = "(none)"; S = "(none)"; N = "No published restriction found at this fix"; R = "" }
}

function Get-FeetList([string]$text) {
    $list = New-Object System.Collections.Generic.List[int]
    if ([string]::IsNullOrWhiteSpace($text) -or $text -eq "(none)") { return $list }
    $matches_ = [regex]::Matches($text, 'FL\s*(\d+)')
    foreach ($m in $matches_) { $list.Add(100 * [int]$m.Groups[1].Value) }
    $matches2 = [regex]::Matches($text, '(?<![A-Z])(\d{3,5})(?!\d)')
    foreach ($m in $matches2) {
        $n = [int]$m.Groups[1].Value
        if ($n -ge 50 -and $n -le 45000) { $list.Add($n) }
    }
    return $list
}

function Compare-Alt($db, $real) {
    $dbN = ($db -eq "(none)" -or [string]::IsNullOrWhiteSpace($db))
    $rN = ($real -eq "(none)" -or [string]::IsNullOrWhiteSpace($real) -or $real.StartsWith("THR"))
    # THR vs AT (runway elev stored in DB)
    $tm = [regex]::Match($real, 'THR\s+(-?\d+)')
    $dm = [regex]::Match($db, 'AT\s+(\d+)')
    if ($tm.Success -and $dm.Success) {
        $diff = [math]::Abs([int]$dm.Groups[1].Value - [int]$tm.Groups[1].Value)
        if ($diff -le 50) { return "Yes (THR close)" }
        return "No (THR vs DB)"
    }
    if ($dbN -and ($real -eq "(none)")) { return "Yes (both none)" }
    if ($dbN -and -not ($real -eq "(none)")) { return "Missing in DB" }
    if (-not $dbN -and ($real -eq "(none)")) { return "Extra in DB" }
    $a = @(Get-FeetList $db)
    $b = @(Get-FeetList $real)
    if ($a.Count -gt 0 -and $b.Count -gt 0) {
        foreach ($x in $a) {
            foreach ($y in $b) {
                if ([math]::Abs($x - $y) -le 100) { return "Yes" }
            }
        }
        return "No"
    }
    return "Check"
}

function Compare-Spd($db, $real) {
    $dbN = ($db -eq "(none)" -or [string]::IsNullOrWhiteSpace($db))
    $rN = ($real -eq "(none)" -or [string]::IsNullOrWhiteSpace($real))
    if ($dbN -and $rN) { return "Yes (both none)" }
    if ($dbN -and -not $rN) { return "Missing in DB" }
    if (-not $dbN -and $rN) { return "Extra in DB" }
    $dn = [regex]::Match($db, '(\d+)').Groups[1].Value
    $rn = [regex]::Match($real, '(\d+)').Groups[1].Value
    if ($dn -and $rn -and $dn -eq $rn) { return "Yes" }
    return "No"
}

# Parse routes
$infoByLevel = @{}
Get-ChildItem -Path $root -Recurse -Filter "Level Info*.asset" | Where-Object { $_.Extension -eq ".asset" } | ForEach-Object {
    $txt = Get-Content $_.FullName -Raw
    $n = Get-YamlField $txt "LevelNumber"
    if (-not $n) { return }
    $infoByLevel[$n] = [pscustomobject]@{
        Destination = Get-YamlField $txt "Destination"
        Star = Get-YamlField $txt "Star"
        Transition = Get-YamlField $txt "Transition"
        Runway = Get-YamlField $txt "Runway"
    }
}

$rows = New-Object System.Collections.Generic.List[object]
Get-ChildItem -Path (Join-Path $root "Level *") -Directory | ForEach-Object {
    if ($_.Name -notmatch '^Level (\d+)$') { return }
    $lvl = [int]$Matches[1]
    if ($lvl -lt 1 -or $lvl -gt 39) { return }
    $routeFile = Get-ChildItem $_.FullName -Filter "*Route*.asset" |
        Where-Object { $_.Name -notlike "*original*" -and $_.Name -notlike "_ *" } |
        Select-Object -First 1
    if (-not $routeFile) { return }
    $txt = Get-Content $routeFile.FullName -Raw
    $blocks = [regex]::Split($txt, "`r?`n  - ID:")
    $seq = 0
    $occ = @{}
    $info = $infoByLevel["$lvl"]
    for ($i = 1; $i -lt $blocks.Count; $i++) {
        $b = $blocks[$i]
        $name = Get-YamlField $b "Name"
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        $spd = Get-YamlField $b "RawSpeed"
        $alt = Get-YamlField $b "RawAltitude"
        $seq++
        if (-not $occ.ContainsKey($name)) { $occ[$name] = 0 }
        $occ[$name]++
        $ch = Get-ChartRow $lvl $name $occ[$name]
        $dbAlt = Format-DbAlt $alt
        $dbSpd = Format-DbSpd $spd
        $rows.Add([pscustomobject]@{
            Level = $lvl
            ICAO = if ($info) { $info.Destination } else { "" }
            STAR = if ($info) { $info.Star } else { "" }
            Transition = if ($info) { $info.Transition } else { "" }
            Runway = if ($info) { $info.Runway } else { "" }
            Seq = $seq
            Waypoint = $name
            DatabaseAltitude = $dbAlt
            RealAltitude = $ch.A
            DatabaseSpeed = $dbSpd
            RealSpeed = $ch.S
            AltitudeCompare = Compare-Alt $dbAlt $ch.A
            SpeedCompare = Compare-Spd $dbSpd $ch.S
            Notes = $ch.N
            Source = $ch.R
        }) | Out-Null
    }
}

$sorted = $rows | Sort-Object Level, Seq
New-Item -ItemType Directory -Force -Path "C:\Users\User\NavigationShare\artifacts" | Out-Null
$sorted | Export-Csv -Path $outCsv -NoTypeInformation -Encoding UTF8

function Escape-XmlText([string]$s) {
    if ($null -eq $s) { return "" }
    return (($s -replace '&', '&amp;') -replace '<', '&lt;' -replace '>', '&gt;' -replace '"', '&quot;')
}

function Get-ColLetter([int]$n) {
    $s = ""
    while ($n -gt 0) {
        $n--
        $s = [char](65 + ($n % 26)) + $s
        $n = [math]::Floor($n / 26)
    }
    return $s
}

function New-InlineCell([string]$ref, [string]$val, [string]$style) {
    $t = Escape-XmlText $val
    $sAttr = if ($style) { " s=`"$style`"" } else { "" }
    return "<c r=`"$ref`" t=`"inlineStr`"$sAttr><is><t xml:space=`"preserve`">$t</t></is></c>"
}

function New-NumberCell([string]$ref, $val, [string]$style) {
    $sAttr = if ($style) { " s=`"$style`"" } else { "" }
    return "<c r=`"$ref`"$sAttr><v>$val</v></c>"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$headers = @(
    "Level","ICAO","STAR","Transition","Runway","Seq","Waypoint",
    "Database altitude restriction","Real altitude restriction",
    "Database speed restriction","Real speed restriction",
    "Altitude compare","Speed compare","Notes","Source"
)
$colCount = $headers.Count
$rowCount = $sorted.Count + 1
$lastCol = Get-ColLetter $colCount
$filterRef = "A1:${lastCol}${rowCount}"

$sheet1 = New-Object System.Text.StringBuilder
[void]$sheet1.AppendLine('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void]$sheet1.AppendLine('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">')
[void]$sheet1.AppendLine('<sheetPr><pageSetUpPr fitToPage="0"/></sheetPr>')
[void]$sheet1.AppendLine("<dimension ref=`"$filterRef`"/>")
[void]$sheet1.AppendLine('<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>')
[void]$sheet1.AppendLine('<sheetFormatPr defaultRowHeight="15"/>')
[void]$sheet1.AppendLine('<cols>')
[void]$sheet1.AppendLine('<col min="1" max="1" width="8" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="2" max="2" width="10" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="3" max="3" width="12" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="4" max="4" width="12" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="5" max="5" width="10" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="6" max="6" width="8" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="7" max="7" width="14" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="8" max="8" width="36" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="9" max="9" width="40" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="10" max="10" width="26" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="11" max="11" width="26" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="12" max="12" width="18" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="13" max="13" width="18" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="14" max="14" width="72" customWidth="1"/>')
[void]$sheet1.AppendLine('<col min="15" max="15" width="38" customWidth="1"/>')
[void]$sheet1.AppendLine('</cols>')
[void]$sheet1.AppendLine('<sheetData>')

# header
[void]$sheet1.Append('<row r="1" ht="20" customHeight="1">')
for ($c = 1; $c -le $colCount; $c++) {
    [void]$sheet1.Append((New-InlineCell ((Get-ColLetter $c) + "1") $headers[$c - 1] "1"))
}
[void]$sheet1.AppendLine('</row>')

$ri = 2
foreach ($row in $sorted) {
    $vals = @(
        $row.Level, $row.ICAO, $row.STAR, $row.Transition, $row.Runway, $row.Seq, $row.Waypoint,
        $row.DatabaseAltitude, $row.RealAltitude, $row.DatabaseSpeed, $row.RealSpeed,
        $row.AltitudeCompare, $row.SpeedCompare, $row.Notes, $row.Source
    )
    [void]$sheet1.Append("<row r=`"$ri`">")
    for ($c = 1; $c -le $colCount; $c++) {
        $ref = (Get-ColLetter $c) + "$ri"
        $style = if ($c -ge 14) { "2" } else { "0" }
        if ($c -eq 1 -or $c -eq 6) {
            [void]$sheet1.Append((New-NumberCell $ref $vals[$c - 1] $style))
        } else {
            [void]$sheet1.Append((New-InlineCell $ref ([string]$vals[$c - 1]) $style))
        }
    }
    [void]$sheet1.AppendLine('</row>')
    $ri++
}
[void]$sheet1.AppendLine('</sheetData>')
[void]$sheet1.AppendLine("<autoFilter ref=`"$filterRef`"/>")
[void]$sheet1.AppendLine('<pageMargins left="0.25" right="0.25" top="0.5" bottom="0.5" header="0.3" footer="0.3"/>')
[void]$sheet1.AppendLine('</worksheet>')

$legend = @(
    @("Column / encoding notes", ""),
    @("", ""),
    @("Scope", "Levels 1-39 main Route assets. _START included. DLE-5 is stored in Level 1 and listed as-is; it is a game DME fix, not treated as a missing published STAR waypoint."),
    @("Database altitude", "RawAltitude: 3000 = AT; 3000A = at or above; 11000B = at or below; 8000A9000B = between."),
    @("Database speed", "RawSpeed 0 = none. Any other value is coded as a MAX restriction in the FMC."),
    @("Real altitude/speed", "From the STAR/IAP matching the game procedure name. Where that name was withdrawn, the historical plate for that name is used."),
    @("THR", "Runway threshold elevation. Not a crossing restriction; compared only when the game stores it as AT altitude."),
    @("Yes (both none)", "Neither database nor chart has a restriction at that fix."),
    @("Missing in DB", "Chart has a restriction the route file does not."),
    @("Extra in DB", "Route file has a restriction the chart does not (often Vapp coded as MAX)."),
    @("Sources", "LVNL EHAM STAR 2026, UK AIP OCK/BNN/ILS, Austro Control LOWW, DHMI LTFJ, Jepp CABIN/SILVA/HKJK, VATSIM Germany ILS-Z, IVAO LFPG memo.")
)

$sheet2 = New-Object System.Text.StringBuilder
[void]$sheet2.AppendLine('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void]$sheet2.AppendLine('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">')
[void]$sheet2.AppendLine('<sheetFormatPr defaultRowHeight="15"/>')
[void]$sheet2.AppendLine('<cols><col min="1" max="1" width="24" customWidth="1"/><col min="2" max="2" width="110" customWidth="1"/></cols>')
[void]$sheet2.AppendLine('<sheetData>')
for ($li = 0; $li -lt $legend.Count; $li++) {
    $r = $li + 1
    $styleA = if ($li -eq 0 -or $legend[$li][0]) { "1" } else { "0" }
    [void]$sheet2.Append("<row r=`"$r`" ht=`"30`" customHeight=`"1`">")
    [void]$sheet2.Append((New-InlineCell ("A$r") $legend[$li][0] $styleA))
    [void]$sheet2.Append((New-InlineCell ("B$r") $legend[$li][1] "2"))
    [void]$sheet2.AppendLine('</row>')
}
[void]$sheet2.AppendLine('</sheetData></worksheet>')

$styles = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="2">
<font><sz val="11"/><name val="Calibri"/></font>
<font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
</fonts>
<fills count="3">
<fill><patternFill patternType="none"/></fill>
<fill><patternFill patternType="gray125"/></fill>
<fill><patternFill patternType="solid"><fgColor rgb="FF1F4E79"/><bgColor indexed="64"/></patternFill></fill>
</fills>
<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="3">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top"/></xf>
<xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf>
</cellXfs>
</styleSheet>
'@

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
<Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>
'@

$rels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
'@

$workbook = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets>
<sheet name="Restrictions" sheetId="1" r:id="rId1"/>
<sheet name="Legend" sheetId="2" r:id="rId2"/>
</sheets>
</workbook>
'@

$workbookRels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
'@

if (Test-Path $outXlsx) { Remove-Item $outXlsx -Force }
$zip = [System.IO.Compression.ZipFile]::Open($outXlsx, [System.IO.Compression.ZipArchiveMode]::Create)
$utf8 = New-Object System.Text.UTF8Encoding $false
function Add-ZipText($archive, [string]$name, [string]$text) {
    $entry = $archive.CreateEntry($name.Replace('\', '/'), [System.IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    $bytes = $utf8.GetBytes($text)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Dispose()
}
try {
    Add-ZipText $zip "[Content_Types].xml" $contentTypes
    Add-ZipText $zip "_rels/.rels" $rels
    Add-ZipText $zip "xl/workbook.xml" $workbook
    Add-ZipText $zip "xl/_rels/workbook.xml.rels" $workbookRels
    Add-ZipText $zip "xl/styles.xml" $styles
    Add-ZipText $zip "xl/worksheets/sheet1.xml" $sheet1.ToString()
    Add-ZipText $zip "xl/worksheets/sheet2.xml" $sheet2.ToString()
}
finally {
    $zip.Dispose()
}

Write-Output "XLSX=$outXlsx"
Write-Output "CSV=$outCsv"
Write-Output "ROWS=$($sorted.Count)"
$sorted | Group-Object AltitudeCompare | ForEach-Object { "ALT $($_.Name)=$($_.Count)" }
$sorted | Group-Object SpeedCompare | ForEach-Object { "SPD $($_.Name)=$($_.Count)" }
