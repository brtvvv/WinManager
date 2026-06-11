$ErrorActionPreference = "Stop"

$repo    = "brtvvv/WinManager"
$api     = "https://api.github.com/repos/$repo/releases/tags/dev-latest"
$release = Invoke-RestMethod -Uri $api -Headers @{ "User-Agent" = "WinManager-Installer" }
$asset   = $release.assets | Where-Object { $_.name -eq "WinManager.exe" }

if (-not $asset) {
    Write-Error "Nie znaleziono WinManager.exe w dev-latest. Sprawdz czy workflow Dev Build przeszedl."
    exit 1
}

$installDir = Join-Path $env:LOCALAPPDATA "WinManager"
$exePath    = Join-Path $installDir "WinManager.exe"

Write-Host "Instalowanie WinManager (DEV build)..." -ForegroundColor Magenta

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $exePath -UseBasicParsing

# Skrot na pulpicie
$shell         = New-Object -ComObject WScript.Shell
$shortcut      = $shell.CreateShortcut("$env:USERPROFILE\Desktop\WinManager DEV.lnk")
$shortcut.TargetPath = $exePath
$shortcut.Save()

Write-Host ""
Write-Host "Gotowe! WinManager DEV zainstalowany w:" -ForegroundColor Green
Write-Host "  $exePath"
Write-Host "Skrot dodany na pulpit."
Write-Host ""
Write-Host "Uruchom jako administrator dla pelnej funkcjonalnosci." -ForegroundColor Yellow
