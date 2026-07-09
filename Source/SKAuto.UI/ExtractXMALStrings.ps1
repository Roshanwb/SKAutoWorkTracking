# Extract and clean strings from XAML files with source tracking (UTF-8 encoding)
$outputFile = "StringsToLocalize_Clean.txt"
$pattern = '(?:Content|Text|Header|ToolTip|Title|Watermark)="([^"]+)"'
$results = @()

Get-ChildItem -Recurse -Filter *.xaml | ForEach-Object {
    $fileName = $_.Name
    $content = Get-Content $_.FullName -Raw -Encoding UTF8  # Add UTF8 encoding
    $matches = [regex]::Matches($content, $pattern)
    foreach ($m in $matches) {
        $value = $m.Groups[1].Value
        if ($value -notmatch '^\{') { # skip bindings
            $results += [PSCustomObject]@{
                File = $fileName
                Text = $value
            }
        }
    }
}

# Clean and deduplicate, keeping file information
$results |
    Where-Object { $_.Text -match '\w' } |
    ForEach-Object { $_.Text = $_.Text.Trim(); $_ } |
    Where-Object { $_.Text -notmatch '^[&#\*•-]' } |
    Sort-Object -Property Text -Unique |
    ForEach-Object { "$($_.File) | $($_.Text)" } |
    Out-File $outputFile -Encoding UTF8  # Add UTF8 encoding

Write-Host "Extracted and cleaned $($results.Count) strings to $outputFile"
