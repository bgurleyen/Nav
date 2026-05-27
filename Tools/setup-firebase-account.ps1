# Firebase setup for NavigationShare
# Account: birolgurleyen@gmail.com
# Run from repo root: powershell -ExecutionPolicy Bypass -File Tools/setup-firebase-account.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$FirebaseDir = Join-Path $RepoRoot "firebase"

Write-Host "=== NavigationShare Firebase Setup ===" -ForegroundColor Cyan
Write-Host "Target account: birolgurleyen@gmail.com"
Write-Host ""

Write-Host "[1/6] Firebase CLI login (browser opens - sign in with birolgurleyen@gmail.com)" -ForegroundColor Yellow
npx --yes firebase-tools@latest login --reauth
if ($LASTEXITCODE -ne 0) { throw "firebase login failed" }

Write-Host ""
Write-Host "[2/6] Listing Firebase projects for logged-in account..." -ForegroundColor Yellow
npx --yes firebase-tools@latest projects:list

$projectId = Read-Host "Enter Firebase project id to use (default: navigationshare-alpha)"
if ([string]::IsNullOrWhiteSpace($projectId)) {
    $projectId = "navigationshare-alpha"
}

Write-Host ""
Write-Host "[3/6] Setting active project: $projectId" -ForegroundColor Yellow
Push-Location $FirebaseDir
npx --yes firebase-tools@latest use $projectId
if ($LASTEXITCODE -ne 0) {
    $create = Read-Host "Project not found. Create it now? (y/n)"
    if ($create -eq "y") {
        npx --yes firebase-tools@latest projects:create $projectId --display-name "NavigationShare"
        npx --yes firebase-tools@latest use $projectId
    } else {
        Pop-Location
        throw "Project not configured"
    }
}

Write-Host ""
Write-Host "[4/6] Deploying Firestore rules..." -ForegroundColor Yellow
npx --yes firebase-tools@latest deploy --only firestore:rules
Pop-Location

Write-Host ""
Write-Host "[5/6] Register apps in Firebase Console (if not done yet):" -ForegroundColor Yellow
Write-Host "  https://console.firebase.google.com/project/$projectId/settings/general"
Write-Host "  - Android package: com.DefaultCompany.Navigation"
Write-Host "  - iOS bundle id:   com.DefaultCompany.Navigation"
Write-Host "  Download:"
Write-Host "    - google-services.json (Android)"
Write-Host "    - GoogleService-Info.plist (iOS)"

$configPath = Read-Host "Path to downloaded google-services.json (or Enter to skip)"
if (-not [string]::IsNullOrWhiteSpace($configPath)) {
    & (Join-Path $RepoRoot "Tools\apply-google-services.ps1") -GoogleServicesJson $configPath
}

Write-Host ""
Write-Host "[6/6] Enable Firestore (if new project):" -ForegroundColor Yellow
Write-Host "  https://console.firebase.google.com/project/$projectId/firestore"
Write-Host "  Create database -> Production mode -> region of your choice"
Write-Host ""
Write-Host "Done. Open Unity, Play, and check Console for 'Firestore Init'." -ForegroundColor Green
