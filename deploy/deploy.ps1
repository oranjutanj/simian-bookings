# =============================================================================
# Simian Bookings - Azure Resource Provisioning Script
# Run once to create Azure infrastructure.
# After provisioning, push to main and GitHub Actions deploys automatically.
# =============================================================================

$ErrorActionPreference = "Stop"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI ('az') is not installed or not on PATH. Install: https://aka.ms/installazurecliwindows"
    exit 1
}

if (-not (Get-Command Set-Clipboard -ErrorAction SilentlyContinue)) {
    Write-Error "Set-Clipboard is unavailable in this shell. Run in Windows PowerShell 5.1 or PowerShell 7 on Windows."
    exit 1
}

# Resource names
$RG       = "simian-bookings-rg"
$LOCATION = "uksouth"
$STORAGE  = "simianbookingsfnsa"   # must be globally unique and lowercase
$FUNC_APP = "simian-bookings-api"
$SWA_NAME = "simian-bookings-web"
$SWA_LOC  = "westeurope"           # SWA not available in uksouth

$scriptDir = $PSScriptRoot
$apiDir = Join-Path $scriptDir "..\api"
$settingsFile = Join-Path $apiDir "local.settings.json"

Write-Host "`n[1/6] Checking Azure login..." -ForegroundColor Cyan
$null = az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Not logged in. Opening browser for login..."
    az login | Out-Null
}
$sub = (az account show | ConvertFrom-Json).name
Write-Host "  Logged in to: $sub" -ForegroundColor Green

Write-Host "`n[2/6] Creating resource group '$RG' in $LOCATION..." -ForegroundColor Cyan
az group create --name $RG --location $LOCATION | Out-Null
Write-Host "  Done." -ForegroundColor Green

Write-Host "`n[3/6] Creating storage account '$STORAGE'..." -ForegroundColor Cyan
az storage account create `
    --name $STORAGE `
    --resource-group $RG `
    --location $LOCATION `
    --sku Standard_LRS `
    --kind StorageV2 | Out-Null
Write-Host "  Done." -ForegroundColor Green

Write-Host "`n[4/6] Creating Function App '$FUNC_APP'..." -ForegroundColor Cyan
az functionapp create `
    --resource-group $RG `
    --consumption-plan-location $LOCATION `
    --runtime dotnet-isolated `
    --runtime-version 8 `
    --functions-version 4 `
    --name $FUNC_APP `
    --storage-account $STORAGE `
    --os-type Windows | Out-Null
Write-Host "  Done." -ForegroundColor Green

Write-Host "`n[5/6] Applying Function App settings..." -ForegroundColor Cyan
if (-not (Test-Path $settingsFile)) {
    Write-Error "Cannot find $settingsFile. Aborting."
    exit 1
}
$vals = (Get-Content $settingsFile | ConvertFrom-Json).Values

$appSettings = @(
    "TenantId=$($vals.TenantId)"
    "ClientId=$($vals.ClientId)"
    "ClientSecret=$($vals.ClientSecret)"
    "CalendarUserId=$($vals.CalendarUserId)"
    "CalendarTimeZone=$($vals.CalendarTimeZone)"
    "GoogleClientId=$($vals.GoogleClientId)"
    "GoogleClientSecret=$($vals.GoogleClientSecret)"
    "GoogleRefreshToken=$($vals.GoogleRefreshToken)"
    "GoogleCalendarId=$($vals.GoogleCalendarId)"
)

az functionapp config appsettings set `
    --name $FUNC_APP `
    --resource-group $RG `
    --settings @appSettings | Out-Null
Write-Host "  Done." -ForegroundColor Green

Write-Host "`n[6/6] Creating Static Web App '$SWA_NAME'..." -ForegroundColor Cyan
az staticwebapp create `
    --name $SWA_NAME `
    --resource-group $RG `
    --location $SWA_LOC `
    --sku Free | Out-Null
$SWA_HOSTNAME = (az staticwebapp show --name $SWA_NAME --resource-group $RG | ConvertFrom-Json).defaultHostname
Write-Host "  SWA URL: https://$SWA_HOSTNAME" -ForegroundColor Green

Write-Host ""
Write-Host "Add these as GitHub repository secrets (Settings -> Secrets -> Actions):" -ForegroundColor Yellow
Write-Host ""

$publishProfile = az functionapp deployment list-publishing-profiles `
    --name $FUNC_APP `
    --resource-group $RG `
    --xml
Write-Host "  Secret name : AZURE_FUNCTIONAPP_PUBLISH_PROFILE" -ForegroundColor White
Write-Host "  Secret value copied to clipboard." -ForegroundColor White
$publishProfile | Set-Clipboard
Read-Host "Press Enter after saving AZURE_FUNCTIONAPP_PUBLISH_PROFILE in GitHub"

$deployToken = az staticwebapp secrets list `
    --name $SWA_NAME `
    --resource-group $RG `
    --query "properties.apiKey" `
    -o tsv
Write-Host "  Secret name : AZURE_STATIC_WEB_APPS_API_TOKEN" -ForegroundColor White
Write-Host "  Secret value copied to clipboard." -ForegroundColor White
$deployToken | Set-Clipboard

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " Provisioning complete" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Function API: https://$FUNC_APP.azurewebsites.net/api"
Write-Host "  Booking page: https://$SWA_HOSTNAME"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Add AZURE_STATIC_WEB_APPS_API_TOKEN to GitHub secrets."
Write-Host "  2. Push to main and GitHub Actions will deploy automatically."
Write-Host "  3. Add custom domain book.simiancoaching.co.uk in SWA settings."
Write-Host "  4. Ensure Mail.Send permission is granted for the app registration."
