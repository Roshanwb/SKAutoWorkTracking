# ReplaceColorsInXaml.ps1
# Replaces hard-coded colors in .xaml files with {DynamicResource} references.
# Correctly replaces the entire attribute assignment, e.g. Background="#2C3E50"
# becomes Background="{DynamicResource PrimaryBackgroundBrush}"

$colorMap = @{
    "#2C3E50" = "PrimaryBackgroundBrush"
    "#1A252F" = "DarkBackgroundBrush"
    "#34495E" = "SecondaryBackgroundBrush"
    "#3F51B5" = "AccentBrush"
    "#2196F3" = "AccentBrush"
    "#3498DB" = "AccentBrush"
    "#27AE60" = "SuccessBrush"
    "#2ECC71" = "SuccessBrush"
    "#F39C12" = "WarningBrush"
    "#E67E22" = "WarningBrush"
    "#E74C3C" = "DangerBrush"
    "#DC3545" = "DangerBrush"
    "#E91E63" = "DangerBrush"
    "#FFFFFF" = "HeaderForegroundBrush"
    "#F8F9FA" = "LightBackgroundBrush"
    "#F5F5F5" = "AlternatingRowBackgroundBrush"
    "#BDC3C7" = "DisabledForegroundBrush"
    "#7F8C8D" = "DisabledForegroundBrush"
    "#DEE2E6" = "BorderBrush"
    "#DDDDDD" = "CardBorderBrush"
    "#E0E0E0" = "LightBorderBrush"
    "#E6FFE6" = "TotalBackgroundBrush"
    "#4CAF50" = "SuccessBrush"
    "#8BC34A" = "SuccessBrush"
    "#17A2B8" = "AccentBrush"
    "#6C757D" = "ButtonSecondaryBackground"
}

# Get all XAML files, excluding theme files themselves (they define the resources)
$xamlFiles = Get-ChildItem -Recurse -Filter *.xaml | Where-Object {
    $_.Name -notmatch 'LightTheme|DarkTheme|BlueTheme'
}

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false

    foreach ($color in $colorMap.Keys) {
        $resourceKey = $colorMap[$color]
        # Build the replacement: attribute name + = + "{DynamicResource Key}"
        # Capture the attribute name (Background, Foreground, BorderBrush, Brush, Color)
        $pattern = '(Background|Foreground|BorderBrush|Brush|Color)="' + [regex]::Escape($color) + '"'
        $replacement = '$1="{DynamicResource ' + $resourceKey + '}"'
        if ($content -match $pattern) {
            $content = $content -replace $pattern, $replacement
            $modified = $true
        }
    }

    if ($modified) {
        Set-Content $file.FullName $content -NoNewline
        Write-Host "Updated $($file.FullName)"
    }
}

Write-Host "Done! Run the repair script below if you previously ran a broken version that removed the equals sign."