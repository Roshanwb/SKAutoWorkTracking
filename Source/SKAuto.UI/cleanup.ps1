# Find all XAML files with non-ASCII characters outside of {loc:Translate ...}
$files = Get-ChildItem -Recurse -Filter *.xaml
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    # Remove all markup extensions like {loc:Translate ...}
    $stripped = $content -replace '{loc:Translate[^}]*}', ''
    # Check for non-ASCII characters (codes > 127)
    if ($stripped -match '[^\x00-\x7F]') {
        Write-Host "Potentially problematic file: $($f.FullName)"
        Write-Host "Line containing non-ASCII:"
        $lines = $content -split "`n"
        for ($i=0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '[^\x00-\x7F]' -and $lines[$i] -notmatch '{loc:Translate[^}]*}') {
                Write-Host "Line $($i+1): $($lines[$i].Trim())"
            }
        }
        Write-Host "---"
    }
}