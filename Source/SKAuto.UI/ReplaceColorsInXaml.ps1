# ReplaceColorsInXaml.ps1
# Replaces hard-coded colors in .xaml files with DynamicResource references

$colorMap = @{
    "#2C3E50" = "{DynamicResource PrimaryBackgroundBrush}"
    "#1A252F" = "{DynamicResource DarkBackgroundBrush}"
    "#34495E" = "{DynamicResource SecondaryBackgroundBrush}"
    "#3F51B5" = "{DynamicResource AccentBrush}"
    "#2196F3" = "{DynamicResource AccentBrush}"
    "#3498DB" = "{DynamicResource AccentBrush}"
    "#27AE60" = "{DynamicResource SuccessBrush}"
    "#2ECC71" = "{DynamicResource SuccessBrush}"
    "#F39C12" = "{DynamicResource WarningBrush}"
    "#E67E22" = "{DynamicResource WarningBrush}"
    "#E74C3C" = "{DynamicResource DangerBrush}"
    "#DC3545" = "{DynamicResource DangerBrush}"
    "#E91E63" = "{DynamicResource DangerBrush}"
    "#FFFFFF" = "{DynamicResource HeaderForegroundBrush}"
    "#F8F9FA" = "{DynamicResource LightBackgroundBrush}"
    "#F5F5F5" = "{DynamicResource AlternatingRowBackgroundBrush}"
    "#BDC3C7" = "{DynamicResource DisabledForegroundBrush}"
    "#7F8C8D" = "{DynamicResource DisabledForegroundBrush}"
    "#DEE2E6" = "{DynamicResource BorderBrush}"
    "#DDDDDD" = "{DynamicResource CardBorderBrush}"
    "#E0E0E0" = "{DynamicResource LightBorderBrush}"
    "#E6FFE6" = "{DynamicResource TotalBackgroundBrush}"
    "#4CAF50" = "{DynamicResource SuccessBrush}"
    "#8BC34A" = "{DynamicResource SuccessBrush}"
    "#17A2B8" = "{DynamicResource AccentBrush}"
    "#6C757D" = "{DynamicResource ButtonSecondaryBackground}"
}

# Get all XAML files
$xamlFiles = Get-ChildItem -Recurse -Filter *.xaml | Where-Object { 
    $_.Name -notmatch 'LightTheme|DarkTheme|BlueTheme'  # Don't modify the theme files themselves
}

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false

    foreach ($color in $colorMap.Keys) {
        $replacement = $colorMap[$color]
        # Pattern: attribute values like Background="#2C3E50"
        $pattern = '(?<=Background|Foreground|BorderBrush|Brush|Color)="' + [regex]::Escape($color) + '"'
        if ($content -match $pattern) {
            $content = $content -replace $pattern, "$replacement"
            $modified = $true
        }
    }

    if ($modified) {
        Set-Content $file.FullName $content -NoNewline
        Write-Host "Updated $($file.FullName)"
    }
}

Write-Host "Done! Remember to also replace font sizes and font families manually."