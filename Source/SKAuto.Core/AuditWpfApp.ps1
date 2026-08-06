$rootDir = Get-Location
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "STARTING FIXED ARCHITECTURAL AUDIT FOR WEBASSEMBLY MIGRATION" -ForegroundColor Cyan
Write-Host "Target Directory: $rootDir" -ForegroundColor Gray
Write-Host "======================================================================" -ForegroundColor Cyan

$auditTargets = [ordered]@{
    "SQLite / Local Databases"    = '(sqlite|microsoft\.data\.sqlite|system\.data\.sqlite|sqliteconnection|dbcontext)'
    "Direct File I/O (System.IO)" = '(File\.(Write|Read|Append)|StreamWriter|StreamReader|Directory\.Create|Environment\.GetFolderPath)'
    "Native Windows APIs (Win32)" = '(\[DllImport|kernel32|user32|gdi32|registry|microsoft\.win32)'
    "WPF Window Management"      = '(\.ShowDialog\(|new\s+Window\(\)|WindowStartupLocation|this\.Close\()'
}

# Grab all .cs files, ignoring build artifact folders
$files = Get-ChildItem -Path $rootDir -Filter "*.cs" -Recurse | 
         Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|packages)\\' }

if ($files.Count -eq 0) {
    Write-Host "No .cs source files found in this directory tree." -ForegroundColor Red
    return
}

foreach ($target in $auditTargets.GetEnumerator()) {
    Write-Host "`nSearching for: $($target.Key)..." -ForegroundColor Yellow
    
    # Use native Select-String to scan files cleanly
    $matchesFound = $files | Select-String -Pattern $target.Value
    
    if ($matchesFound) {
        Write-Host "Found $($matchesFound.Count) instance(s):" -ForegroundColor Red
        
        $matchesFound | ForEach-Object {
            [PSCustomObject]@{
                File        = $_.Path.Replace($rootDir.Path, "")
                Line        = $_.LineNumber
                CodeSnippet = $_.Line.Trim()
            }
        } | Format-Table File, Line, CodeSnippet -AutoSize | Out-String | Write-Host
    } else {
        Write-Host "Clean! No matches found." -ForegroundColor Green
    }
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host "AUDIT COMPLETE." -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan