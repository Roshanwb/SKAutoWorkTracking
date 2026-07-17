# WrapStaticCalls.ps1
$files = Get-ChildItem "*.cs"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw

    # Patterns to wrap (single-line statements)
    $patterns = @(
        'MessageBox\.Show',
        'new OpenFileDialog',
        'new SaveFileDialog',
        'new OpenFolderDialog',
        'Application\.Current\.Dispatcher',
        'Mouse\.OverrideCursor\s*=',
        '\.Close\(\)',
        '\.DialogResult\s*=',
        '\.DataContext\s*=',
        '\.Owner\s*=',
        'WindowStartupLocation',
        '\.ShowDialog\(\)'
    )

    foreach ($pattern in $patterns) {
        $content = $content -replace "(?<!(#if\s+!OPENSILVER\s*\n))($pattern.*?;)",
                                   "#if !OPENSILVER`n`$1`n#endif"
    }

    Set-Content $file.FullName $content
}