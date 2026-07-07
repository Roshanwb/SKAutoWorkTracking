# SafeReplaceStringsInXaml.ps1
# Replaces hard-coded strings with {loc:Translate Key} while preserving quotes

$csvPath = "StringKeyMapping.csv"
$mappings = Import-Csv $csvPath

# Build a hash map for fast lookup
$map = @{}
foreach ($m in $mappings) {
    $raw = $m.RawString -replace '"', ''  # remove surrounding quotes if any
    $key = $m.Key
    $map[$raw] = $key
}

# Get all XAML files
$xamlFiles = Get-ChildItem -Recurse -Filter *.xaml

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw

    # For each string, replace inside quoted attributes
    foreach ($raw in $map.Keys) {
        $key = $map[$raw]
        # Escape regex special characters in the raw string
        $escaped = [regex]::Escape($raw)
        # Replace occurrences that are inside quotes: e.g., ToolTip="RawString"
        # We'll use a regex that captures the attribute name and the surrounding quotes
        # Pattern: (Content|ToolTip|Header|Text|Title|Watermark)="RawString"
        # Replace with: $1="{loc:Translate Key}"
        $pattern = "(?<attr>(Content|ToolTip|Header|Text|Title|Watermark))=`"$escaped`""
        $replacement = '${attr}="{loc:Translate ' + $key + '}"'
        $content = $content -replace $pattern, $replacement
    }

    Set-Content $file.FullName $content -NoNewline
    Write-Host "Updated $($file.FullName)"
}

Write-Host "Done!"