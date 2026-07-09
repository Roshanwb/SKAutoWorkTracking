# Extract hard-coded strings from .cs files (excluding design files)
$output = "CSharpStrings.txt"
$patterns = @(
    'MessageBox\.Show\("([^"]+)"',
    'StatusMessage\s*=\s*"([^"]+)"',
    '_logger\.(?:LogInfo|LogWarning|LogError)\("([^"]+)"',
    '_loggingService\.(?:LogInfo|LogWarning|LogError)\("([^"]+)"',
    'throw new Exception\("([^"]+)"',
    'new ArgumentException\("([^"]+)"'
)

$allMatches = @()

Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.Name -notmatch "\.Designer\.cs$" } | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    foreach ($pattern in $patterns) {
        $matches = [regex]::Matches($content, $pattern)
        foreach ($m in $matches) {
            $allMatches += [PSCustomObject]@{
                File = $_.FullName
                String = $m.Groups[1].Value
            }
        }
    }
}

$allMatches | Group-Object String | Select-Object Name, Count | Sort-Object Name | Out-File $output
Write-Host "Extracted $($allMatches.Count) strings to $output"