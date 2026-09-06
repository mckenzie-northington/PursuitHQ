# ---------------------------------------------------------------------------
# PursuitHQ - start everything
#
# Opens two PowerShell windows (the API and the website), waits for them to
# boot, then opens the site in your browser.
#
# To run: right-click this file -> "Run with PowerShell"
#   or from a terminal in this folder:  .\start-dev.ps1
#
# To stop everything: close both PowerShell windows, or press Ctrl+C in each.
# ---------------------------------------------------------------------------

$root = $PSScriptRoot

$apiPath = Join-Path $root "Backend\PursuitHQ\PursuitHQ.API"
$webPath = Join-Path $root "Frontend\pursuithq-web"

Write-Host ""
Write-Host "  PursuitHQ" -ForegroundColor Cyan
Write-Host "  ---------" -ForegroundColor Cyan
Write-Host ""

# --- 1. The API -------------------------------------------------------------
Write-Host "  Starting API..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "`$Host.UI.RawUI.WindowTitle = 'PursuitHQ API (port 5051)'; Set-Location '$apiPath'; dotnet run"
)

Start-Sleep -Seconds 6

# --- 2. The website ---------------------------------------------------------
Write-Host "  Starting website..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "`$Host.UI.RawUI.WindowTitle = 'PursuitHQ Website (port 3000)'; Set-Location '$webPath'; npm run dev"
)

Start-Sleep -Seconds 8

# --- 3. Open the browser ----------------------------------------------------
Write-Host "  Opening browser..." -ForegroundColor Yellow
Start-Process "http://localhost:3000"

Write-Host ""
Write-Host "  Running:" -ForegroundColor Green
Write-Host "    Website    http://localhost:3000"
Write-Host "    API docs   http://localhost:5051/swagger"
Write-Host ""
Write-Host "  Two PowerShell windows opened - leave both running." -ForegroundColor DarkGray
Write-Host "  If the site says it cannot reach the API, check the API window for errors." -ForegroundColor DarkGray
Write-Host ""
