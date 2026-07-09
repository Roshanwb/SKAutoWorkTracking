# ReplaceStringsInCS.ps1
# Replaces hard-coded strings in .cs files with LocalizationManager.Instance["Key"]

Add-Type -AssemblyName System.Xml.Linq
$resxPath = "E:\Non Theocratic\SK Auto\Program\SKAutoWorkTracking\Source\SKAuto.UI\Localization\Strings.resx"

# Load the resx file
$xml = [System.Xml.Linq.XDocument]::Load($resxPath)
$ns = $xml.Root.GetDefaultNamespace()
$entries = $xml.Descendants($ns + "data") | Where-Object { $_.Attribute("name") -and $_.Element($ns + "value") }

# Build a map: raw string -> key
$map = @{}
foreach ($entry in $entries) {
    $key = $entry.Attribute("name").Value
    $value = $entry.Element($ns + "value").Value
    # Escape the value for regex
    $map[$value] = $key
}

# Get all .cs files (excluding designer files)
$csFiles = Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.Name -notmatch '\.Designer\.cs$' -and $_.Name -notmatch '\.g\.cs$' }

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false

    foreach ($raw in $map.Keys) {
        $key = $map[$raw]
        # Escape regex special characters in the raw string
        $escaped = [regex]::Escape($raw)
        # Pattern: match the exact string literal (including quotes)
        # We need to avoid replacing inside existing LocalizationManager calls
        $pattern = "(?<![.\w])`"$escaped`""
        $replacement = "LocalizationManager.Instance[`"$key`"]"
        if ($content -match $pattern) {
            $content = $content -replace $pattern, $replacement
            $modified = $true
        }
    }

    if ($modified) {
        # Also add the using directive if not already present
        if ($content -notmatch 'using SKAuto.UI.Localization;') {
            # Insert after the last using
            $content = $content -replace '(using .+;\s*)+', "`$&using SKAuto.UI.Localization;`n"
        }
        Set-Content $file.FullName $content -NoNewline
        Write-Host "Updated $($file.FullName)"
    }
}

Write-Host "Done!"