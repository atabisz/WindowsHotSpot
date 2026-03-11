$ErrorActionPreference = "Stop"

$publishDir = "WindowsHotSpot\bin\Release\net10.0-windows\win-x64\publish"

Write-Host "Publishing self-contained single-file..." -ForegroundColor Cyan
dotnet publish WindowsHotSpot\WindowsHotSpot.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Published to: $publishDir" -ForegroundColor Green
$exeSize = (Get-Item "$publishDir\WindowsHotSpot.exe").Length / 1MB
Write-Host "Exe size: $([math]::Round($exeSize, 1)) MB" -ForegroundColor Green

# Build installer (requires Inno Setup)
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    Write-Host "Building installer..." -ForegroundColor Cyan
    & $iscc "installer\WindowsHotSpot.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }
    Write-Host "Installer created in dist\" -ForegroundColor Green
} else {
    Write-Host "Inno Setup not found. Install with: choco install innosetup -y" -ForegroundColor Yellow
    Write-Host "Skipping installer build." -ForegroundColor Yellow
}
