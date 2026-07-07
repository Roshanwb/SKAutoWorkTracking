# ReplaceStringsInXaml.ps1
# Replaces hard-coded strings with {loc:Translate Key} using the mapping CSV

$csvPath = "StringKeyMapping.csv"
$mappings = Import-Csv $csvPath

# Group by string to avoid duplicate replacements
$replacements = $mappings | Group-Object RawString | ForEach-Object {
    $key = $_.Group[0].Key
    $raw = $_.Name -replace '"', ''
    @{ Raw = $raw; Key = $key }
}

# Get all XAML files
$xamlFiles = Get-ChildItem -Recurse -Filter *.xaml

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw

    foreach ($r in $replacements) {
        # Replace exact string with markup extension
        # We need to match the exact string with quotes
        $pattern = "`"$($r.Raw)`""
        $replacement = "{loc:Translate $($r.Key)}"
        $content = $content -replace $pattern, $replacement
    }

    Set-Content $file.FullName $content -NoNewline
    Write-Host "Updated $($file.FullName)"
}

Write-Host "Done!"