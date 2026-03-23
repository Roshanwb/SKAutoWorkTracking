$output = ".\publish\win-x64"
$sources = "--source https://api.nuget.org/v3/index.json"

# Clean and restore
dotnet clean
dotnet restore $sources

# Publish self-contained
dotnet publish SKAuto.UI\SKAuto.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output $sources

# If success, compress
if ($LASTEXITCODE -eq 0) {
    Compress-Archive -Path "$output\*" -DestinationPath "SKAutoWorkTracking-v1.0.0-win-x64.zip"
    Write-Host "✅ Release package created: SKAutoWorkTracking-v1.0.0-win-x64.zip"
} else {
    Write-Host "❌ Publish failed. Check errors above."
}