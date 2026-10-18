function Get-Atmo([double]$alt_m) {
    $g = 9.80665
    $R = 287.05
    if ($alt_m -lt 11000) {
        $T = 288.15 - 0.0065 * $alt_m
        $P = 101325.0 * [Math]::Pow($T / 288.15, 5.25588)
    } else {
        $T = 216.65
        $P11 = 101325.0 * [Math]::Pow(216.65 / 288.15, 5.25588)
        $P = $P11 * [Math]::Exp(-$g * ($alt_m - 11000.0) / ($R * $T))
    }
    $rho = $P / ($R * $T)
    return @{ T = $T; P = $P; rho = $rho }
}

function Get-Speed([double]$cas_kt, [double]$alt_m) {
    $a0 = 340.294
    $P0 = 101325.0
    $cas = $cas_kt * 0.514444
    $qc = $P0 * ([Math]::Pow((($cas / $a0) * ($cas / $a0)) / 5.0 + 1.0, 3.5) - 1.0)
    $at = Get-Atmo $alt_m
    $M = [Math]::Sqrt(5.0 * ([Math]::Pow($qc / $at.P + 1.0, 1.0 / 3.5) - 1.0))
    $aSound = [Math]::Sqrt(1.4 * 287.05 * $at.T)
    $tas = $M * $aSound
    $q = 0.5 * $at.rho * $tas * $tas
    return @{ M = $M; tas = $tas; q = $q; rho = $at.rho; sigma = ($at.rho / 1.225) }
}

$G = 9.81
$Wn = 75000.0 * $G
$S = 125.0
$Cd0 = 0.020
$K = 0.045
$alts = @(0, 5000, 10000, 15000, 20000, 25000, 30000, 35000, 40000)
$vsT = @(1300, 1400, 1500, 1600, 1700, 1775, 1850, 2500, 3150)

"alt   Mach    q      D      Tneed   sigma  VS3"
$rows = @()
for ($i = 0; $i -lt $alts.Count; $i++) {
    $ft = $alts[$i]
    $sp = Get-Speed 280 ($ft * 0.3048)
    $Cl = $Wn / ($sp.q * $S)
    $Cd = $Cd0 + $K * $Cl * $Cl
    $D = $sp.q * $S * $Cd
    $vsms = $vsT[$i] * 0.3048 / 60.0
    $Tneed = $D - ($vsms / $sp.tas) * $Wn
    $sin3 = [Math]::Sin(3.0 * [Math]::PI / 180.0)
    $vs3 = ($sp.tas * $sin3 / 0.3048) * 60.0
    "{0,6} {1,6:N3} {2,7:N0} {3,7:N0} {4,7:N0} {5,6:N3} {6,6:N0}" -f $ft, $sp.M, $sp.q, $D, $Tneed, $sp.sigma, $vs3
    $rows += @{ ft = $ft; M = $sp.M; q = $sp.q; D = $D; T = $Tneed; sigma = $sp.sigma; tas = $sp.tas; vs3 = $vs3 }
}

# Fit T = Tsl * sigma^a * (1 - b*M^2) using SL and two points by scanning
$Tsl = $rows[0].T
"Tsl=$([Math]::Round($Tsl,0))"
$best = 1e9
$bestA = 0
$bestB = 0
for ($ai = 0; $ai -le 40; $ai++) {
    $a = $ai * 0.05
    for ($bi = 0; $bi -le 40; $bi++) {
        $b = $bi * 0.05
        $err = 0.0
        foreach ($r in $rows) {
            $pred = $Tsl * [Math]::Pow($r.sigma, $a) * (1.0 - $b * $r.M * $r.M)
            $err += [Math]::Abs($pred - $r.T)
        }
        if ($err -lt $best) { $best = $err; $bestA = $a; $bestB = $b }
    }
}
"best a=$bestA b=$bestB meanAbs=$([Math]::Round($best/$rows.Count,0)) N"
"alt   Tneed  Tpred   VSpred  target"
for ($i = 0; $i -lt $rows.Count; $i++) {
    $r = $rows[$i]
    $pred = $Tsl * [Math]::Pow($r.sigma, $bestA) * (1.0 - $bestB * $r.M * $r.M)
    $sinG = ($pred - $r.D) / $Wn
    $vs = - ($r.tas * $sinG / 0.3048) * 60.0
    "{0,6} {1,7:N0} {2,7:N0} {3,7:N0} {4,6}" -f $r.ft, $r.T, $pred, $vs, $vsT[$i]
}
