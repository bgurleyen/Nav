param(
    [Parameter(Mandatory = $true)]
    [string]$GoogleServicesJson,

    [string]$GoogleServicePlist = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (-not (Test-Path $GoogleServicesJson)) {
    throw "File not found: $GoogleServicesJson"
}

$destJson = Join-Path $RepoRoot "Assets\StreamingAssets\google-services.json"
$destDesktop = Join-Path $RepoRoot "Assets\StreamingAssets\google-services-desktop.json"
$destPlist = Join-Path $RepoRoot "Assets\GoogleService-Info.plist"
$androidXml = Join-Path $RepoRoot "Assets\Plugins\Android\FirebaseApp.androidlib\res\values\google-services.xml"
$generatePy = Join-Path $RepoRoot "Assets\Firebase\Editor\generate_xml_from_google_services_json.py"

Copy-Item $GoogleServicesJson $destJson -Force
Write-Host "Updated: $destJson"

if ($GoogleServicePlist -and (Test-Path $GoogleServicePlist)) {
    Copy-Item $GoogleServicePlist $destPlist -Force
    Write-Host "Updated: $destPlist"

    if (Test-Path $generatePy) {
        python $generatePy -i $destPlist -o $destDesktop
        Write-Host "Generated: $destDesktop"
    }
} elseif (Test-Path $generatePy) {
    python $generatePy -i $destJson -o $destDesktop -a $androidXml
    Write-Host "Generated: $destDesktop"
    Write-Host "Generated: $androidXml"
}

$config = Get-Content $destJson -Raw | ConvertFrom-Json
$projectId = $config.project_info.project_id
Write-Host ""
Write-Host "Project id: $projectId" -ForegroundColor Green
Write-Host "Reimport Unity project or restart Unity Editor."
