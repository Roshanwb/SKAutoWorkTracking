# Create-Portable.ps1
# Run this script from the solution root (where the .sln file is)

$ErrorActionPreference = "Stop"
$output = ".\publish\portable"
$zipName = "SKAutoWorkTracking-Portable.zip"

Write-Host "Building self-contained portable executable..." -ForegroundColor Cyan

# Publish self-contained single file
dotnet publish Source/SKAuto.UI/SKAuto.UI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $output

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed. Check errors above." -ForegroundColor Red
    exit 1
}

Write-Host "Build successful." -ForegroundColor Green

# Optional: Copy extra files (like logo.png) if they exist
$extraFiles = @(
    "Source\SKAuto.UI\logo.png",
    "Source\SKAuto.UI\logo.ico"
)
foreach ($file in $extraFiles) {
    if (Test-Path $file) {
        Copy-Item $file -Destination $output
        Write-Host "   Copied $file"
    }
}

# Create ZIP
if (Test-Path $zipName) { Remove-Item $zipName }
Compress-Archive -Path "$output\*" -DestinationPath $zipName -Force

# Report size
$sizeMB = [math]::Round((Get-Item $zipName).Length / 1MB, 2)
Write-Host "Portable ZIP created: $zipName" -ForegroundColor Green
Write-Host "Size: $sizeMB MB" -ForegroundColor Cyan